using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;
using Server.Infrastructure.Services.Import;

namespace Server.Infrastructure.Services;

/// <summary>What importing one ledger leg would do to the account.</summary>
public enum LegStatus
{
    /// <summary>Not in the account yet: it will be inserted.</summary>
    New,
    /// <summary>Already imported (same import key), unchanged: skipped.</summary>
    Duplicate,
    /// <summary>Already imported but its source row changed timing (e.g. pending → confirmed): refreshed in place.</summary>
    Update,
    /// <summary>
    /// Already in the account from an import made before import keys existed: linked to this row
    /// (and corrected to its exact values) instead of being inserted a second time.
    /// </summary>
    Adopt
}

/// <summary>One leg of an import plan and what will happen to it.</summary>
public sealed record PlannedLeg(ImportLeg Leg, LegStatus Status, Transaction? Existing, bool Staked);

/// <summary>One CSV row of an import plan: its legs, or why it is rejected.</summary>
public sealed record PlannedRow(ParsedRow Row, IReadOnlyList<PlannedLeg> Legs);

/// <summary>One file of an import plan.</summary>
public sealed record PlannedFile(string FileName, ParsedFile Parsed, IReadOnlyList<PlannedRow> Rows);

/// <summary>An import worked out against the account's current transactions, before anything is written.</summary>
public sealed record ImportPlan(int AccountId, IReadOnlyList<PlannedFile> Files)
{
    /// <summary>Every leg that will be written or skipped, across all files.</summary>
    public IEnumerable<PlannedLeg> Legs => Files.SelectMany(f => f.Rows).SelectMany(r => r.Legs);
}

/// <summary>A file to import plus which of its rows to take (null = all) and which outflows are staked.</summary>
public sealed record ImportFileRequest(string FileName, string Content, ISet<int>? SelectedRows = null, ISet<int>? StakedRows = null);

/// <summary>
/// CSV import. Parsing is pure (<see cref="CsvImportParser"/>); this service plans the parsed legs
/// against the account (<see cref="PlanAsync"/>) and writes a plan atomically (<see cref="ApplyAsync"/>).
/// A preview and an import run the very same plan, so what is previewed is what gets imported.
/// Idempotency: every leg carries a key built from the source's immutable fields and the database
/// holds it unique per account, so re-importing the same or overlapping exports never duplicates.
/// </summary>
public sealed class ImporterService(CapitrackDbContext db) : IImporterService
{
    public DetectResultDto Detect(string content)
    {
        var (records, headers) = CsvImportParser.ReadCsv(content);
        if (records.Count == 0) return new DetectResultDto("unknown", []);
        return new DetectResultDto(CsvImportParser.DetectFormat(headers), headers);
    }

    /// <summary>Plans importing <paramref name="files"/> into an account; writes nothing.</summary>
    public async Task<ImportPlan> PlanAsync(int accountId, IReadOnlyList<ImportFileRequest> files, CancellationToken ct = default)
    {
        var existing = await db.Transactions.Where(t => t.AccountId == accountId).ToListAsync(ct);
        var byKey = existing.Where(t => t.ImportKey != null).ToDictionary(t => t.ImportKey!, StringComparer.Ordinal);
        var legacy = existing.Where(t => t.ImportKey == null).ToList(); // candidates for adoption, consumed as matched
        var plannedKeys = new HashSet<string>(StringComparer.Ordinal);  // keys already taken earlier in this batch

        var plannedFiles = new List<PlannedFile>(files.Count);
        foreach (var file in files)
        {
            var parsed = CsvImportParser.Parse(file.Content);
            var rows = new List<PlannedRow>(parsed.Rows.Count);
            foreach (var row in parsed.Rows)
            {
                if (row.IsRejected || (file.SelectedRows is not null && !file.SelectedRows.Contains(row.Row)))
                {
                    rows.Add(new PlannedRow(row, []));
                    continue;
                }
                var staked = file.StakedRows?.Contains(row.Row) == true;
                var legs = new List<PlannedLeg>(row.Legs.Count);
                foreach (var leg in row.Legs)
                {
                    if (!plannedKeys.Add(leg.Key))
                        legs.Add(new PlannedLeg(leg, LegStatus.Duplicate, null, false));
                    else if (byKey.TryGetValue(leg.Key, out var keyed))
                        legs.Add(new PlannedLeg(leg, TimingChanged(keyed, leg) ? LegStatus.Update : LegStatus.Duplicate, keyed, false));
                    else if (TakeLegacyMatch(legacy, leg) is { } adopted)
                        legs.Add(new PlannedLeg(leg, LegStatus.Adopt, adopted, false));
                    else
                        legs.Add(new PlannedLeg(leg, LegStatus.New, null, staked && leg.Type == "transfer_out"));
                }
                rows.Add(new PlannedRow(row, legs));
            }
            plannedFiles.Add(new PlannedFile(file.FileName, parsed, rows));
        }
        return new ImportPlan(accountId, plannedFiles);
    }

    /// <summary>
    /// Writes a plan in one database transaction: all of it or none of it. The unique
    /// (account, import key) index backs the planner up if a concurrent import raced it.
    /// </summary>
    public async Task<ImportResultDto> ApplyAsync(ImportPlan plan, CancellationToken ct = default)
    {
        int imported = 0, skipped = 0, updated = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var p in plan.Legs)
            {
                var leg = p.Leg;
                switch (p.Status)
                {
                    case LegStatus.New:
                        db.Transactions.Add(Transaction.Create(
                            plan.AccountId, Symbol.Create(leg.Symbol), TransactionType.From(leg.Type), Quantity.Create(leg.Quantity),
                            leg.Price, leg.Fee, CurrencyCode.Create(leg.Currency), TradeDate.Create(leg.Date), leg.Notes,
                            isStaked: p.Staked, occurredAt: leg.OccurredAt, externalId: leg.ExternalId, importKey: leg.Key));
                        imported++;
                        break;
                    case LegStatus.Update:
                        p.Existing!.RefreshImport(TradeDate.Create(leg.Date), leg.OccurredAt, Quantity.Create(leg.Quantity), leg.Price, leg.Fee);
                        updated++;
                        break;
                    case LegStatus.Adopt:
                        p.Existing!.AdoptImport(leg.Key, leg.ExternalId, leg.OccurredAt, TradeDate.Create(leg.Date),
                            Quantity.Create(leg.Quantity), leg.Price, leg.Fee);
                        skipped++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException e)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Result(plan, 0, 0, 0, [$"Import aborted, nothing was written: {e.InnerException?.Message ?? e.Message}"]);
        }
        return Result(plan, imported, skipped, updated, []);
    }

    private static ImportResultDto Result(ImportPlan plan, int imported, int skipped, int updated, List<string> errors)
    {
        var rows = plan.Files.SelectMany(f => f.Rows.Select(r => (f.FileName, r.Row))).ToList();
        var rejections = rows.Where(x => x.Row.IsRejected)
            .Select(x => plan.Files.Count > 1 ? $"{x.FileName}: row {x.Row.Row}: {x.Row.Rejection}" : $"Row {x.Row.Row}: {x.Row.Rejection}")
            .ToList();
        var format = plan.Files.Count == 1 ? plan.Files[0].Parsed.Format : "bulk";
        return new ImportResultDto(imported, skipped, rows.Count, errors, format, rejections.Count, rejections, updated);
    }

    /// <summary>A keyed transaction needs refreshing when its source row's timing changed (pending → confirmed).</summary>
    private static bool TimingChanged(Transaction existing, ImportLeg leg) =>
        leg.OccurredAt is { } at && existing.OccurredAt != at;

    /// <summary>
    /// Finds (and removes from the pool) a transaction imported before import keys existed that is
    /// this leg: same symbol and type, the same quantity (older imports stored ~15 significant
    /// digits) and the leg's UTC or local date (older Trezor imports used the local date).
    /// </summary>
    private static Transaction? TakeLegacyMatch(List<Transaction> legacy, ImportLeg leg)
    {
        for (var i = 0; i < legacy.Count; i++)
        {
            var t = legacy[i];
            if (t.Symbol.Value != leg.Symbol || t.Type.Value != leg.Type) continue;
            if (t.Date.Value != leg.Date && t.Date.Value != leg.LocalDate) continue;
            var a = t.Quantity.Value;
            var b = leg.Quantity;
            if (Math.Abs(a - b) > Math.Max(Math.Abs(a), Math.Abs(b)) * 0.000000000001m) continue;
            legacy.RemoveAt(i);
            return t;
        }
        return null;
    }

    // ---- IImporterService entry points ----

    public async Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint)
    {
        var plan = await PlanAsync(accountId, [new ImportFileRequest("import.csv", content)]);
        var file = plan.Files[0].Parsed;
        if (file.Rows.Count == 0) return new ImportResultDto(0, 0, 0, [], file.Format);
        if (!CsvImportParser.KnownFormats.Contains(file.Format))
            return new ImportResultDto(0, 0, file.Rows.Count, [$"Unknown CSV format. Headers: {string.Join(", ", file.Headers)}"], "unknown");
        return await ApplyAsync(plan);
    }

    public async Task<PreviewFileDto> PreviewAsync(string fileName, string content, int accountId)
    {
        var plan = await PlanAsync(accountId, [new ImportFileRequest(fileName, content)]);
        var account = await db.Accounts.FindAsync(accountId);
        var accountIsCrypto = account?.Type.Value is "crypto";
        var file = plan.Files[0];

        var legs = new List<PreviewTransactionDto>();
        foreach (var leg in file.Rows.SelectMany(r => r.Legs))
        {
            var l = leg.Leg;
            legs.Add(new PreviewTransactionDto(
                legs.Count, l.Symbol, l.Type, l.Quantity, l.Price, l.Fee, l.Currency, l.Date, l.Notes,
                leg.Status is LegStatus.Duplicate or LegStatus.Adopt,
                l.Type == "transfer_out" && (l.CanStake || accountIsCrypto), l.OccurredAt, l.ExternalId, l.Key));
        }
        var rejected = file.Rows.Where(r => r.Row.IsRejected).Select(r => new RejectedRowDto(r.Row.Row, r.Row.Rejection!)).ToList();
        return new PreviewFileDto(fileName, file.Parsed.Format, legs, rejected);
    }

    public async Task<ImportResultDto> ImportSelectedAsync(int accountId, IEnumerable<SelectedTransactionDto> transactions)
    {
        // legs chosen in a preview, identified by their import keys
        var list = transactions.ToList();
        var existingKeys = (await db.Transactions.Where(t => t.AccountId == accountId && t.ImportKey != null)
            .Select(t => t.ImportKey!).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var legs = list.Select(tx => new PlannedLeg(
            new ImportLeg(tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Fee, tx.Currency, tx.Date, tx.Notes ?? "", tx.OccurredAt, tx.ExternalId,
                Key: tx.ImportKey ?? ""),
            tx.ImportKey is { Length: > 0 } k && !existingKeys.Add(k) ? LegStatus.Duplicate : LegStatus.New,
            null, tx.IsStaked && tx.Type == "transfer_out")).ToList();
        var parsed = new ParsedFile("selected", [], []);
        var plan = new ImportPlan(accountId, [new PlannedFile("selected", parsed, [new PlannedRow(new ParsedRow(0, [], null), legs)])]);
        return await ApplyAsync(plan);
    }
}
