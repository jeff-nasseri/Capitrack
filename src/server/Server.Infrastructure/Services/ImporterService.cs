using System.Collections.Concurrent;
using System.Globalization;
using Server.Domain.Holdings;
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
public sealed class ImporterService(CapitrackDbContext db, IMarketDataService? market = null) : IImporterService
{
    /// <summary>Market prices already looked up (symbol|UTC hour|currency), so a preview and its import price legs identically.</summary>
    private static readonly ConcurrentDictionary<string, (decimal Price, string Source)> PriceMemo = new();

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
            var parsed = await PriceUnpricedAsync(CsvImportParser.Parse(file.Content), ct);
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

    /// <summary>
    /// Legs whose source gives no value (e.g. Revolut commodity exchanges) are priced at the market price of
    /// their time: the hourly candle when a provider has one, otherwise the close of that UTC day (the
    /// documented fallback), converted to the leg's currency. A fee stated in the asset's units is valued at
    /// that price. Legs no provider can price keep a price of 0.
    /// </summary>
    private async Task<ParsedFile> PriceUnpricedAsync(ParsedFile parsed, CancellationToken ct)
    {
        if (market is null || !parsed.Rows.Any(r => r.Legs.Any(l => l.NeedsPrice))) return parsed;
        var rows = new List<ParsedRow>(parsed.Rows.Count);
        foreach (var row in parsed.Rows)
        {
            if (!row.Legs.Any(l => l.NeedsPrice)) { rows.Add(row); continue; }
            var legs = new List<ImportLeg>(row.Legs.Count);
            foreach (var leg in row.Legs)
                legs.Add(leg.NeedsPrice && await MarketPriceAsync(leg, ct) is { } p
                    ? leg with { Price = p.Price, Fee = leg.Fee + Math.Round(leg.UnitFee * p.Price, 2), Notes = $"{leg.Notes} · priced at {p.Source}" }
                    : leg);
            rows.Add(row with { Legs = legs });
        }
        return parsed with { Rows = rows };
    }

    private async Task<(decimal Price, string Source)?> MarketPriceAsync(ImportLeg leg, CancellationToken ct)
    {
        var at = leg.OccurredAt ?? DateTime.SpecifyKind(DateTime.ParseExact(leg.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddHours(12), DateTimeKind.Utc);
        var key = $"{leg.Symbol}|{at:yyyy-MM-ddTHH}|{leg.Currency}";
        if (PriceMemo.TryGetValue(key, out var known)) return known;
        try
        {
            if (await market!.PriceAtAsync(leg.Symbol, at, null, ct) is not { Price: > 0 } point) return null;
            var rate = point.Currency.Equals(leg.Currency, StringComparison.OrdinalIgnoreCase)
                ? 1m : await market.FxRateAsync(point.Currency, leg.Currency, DateOnly.FromDateTime(at), ct);
            if (rate is null) return null;
            var source = point.Resolution == "hour"
                ? $"the {point.At:yyyy-MM-dd HH:mm} UTC hourly price ({point.Provider})"
                : $"the {point.At:yyyy-MM-dd} daily close ({point.Provider})";
            return PriceMemo[key] = (Math.Round(point.Price * rate.Value, 8), source);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null; // unpriced this time; a later preview or import tries again
        }
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

    public async Task<ImportResultDto> ImportFilesAsync(int accountId, IReadOnlyList<ImportFileInput> files) =>
        await ApplyAsync(await PlanAsync(accountId, files.Select(ToRequest).ToList()));

    public async Task<ImportPreviewDto> PreviewAsync(int accountId, IReadOnlyList<ImportFileInput> files)
    {
        var plan = await PlanAsync(accountId, files.Select(ToRequest).ToList());
        var account = await db.Accounts.FindAsync(accountId);
        var accountIsCrypto = account?.Type.Value is "crypto";

        // balances now, and after the plan is applied (updated/adopted rows take their new values,
        // new legs are added) — computed by the same holdings rules the app displays
        var current = await db.Transactions.Where(t => t.AccountId == accountId).ToListAsync();
        var replaced = plan.Legs.Where(l => l.Status is LegStatus.Update or LegStatus.Adopt)
            .ToDictionary(l => l.Existing!.Id, l => l);
        var simulated = current
            .Select(t => replaced.TryGetValue(t.Id, out var pl) ? Transient(accountId, pl.Leg, t.IsStaked) : t)
            .Concat(plan.Legs.Where(l => l.Status == LegStatus.New).Select(l => Transient(accountId, l.Leg, l.Staked)))
            .ToList();
        var before = Balances(current);
        var after = Balances(simulated);

        var result = new List<FilePreviewDto>(plan.Files.Count);
        foreach (var file in plan.Files)
        {
            var rows = new List<PreviewRowDto>(file.Rows.Count);
            foreach (var pr in file.Rows)
            {
                var row = pr.Row;
                if (row.IsRejected)
                {
                    rows.Add(new PreviewRowDto(row.Row, "rejected", row.Rejection, null, null, false, []));
                    continue;
                }
                var legs = row.Legs.Select((leg, i) => new PreviewLegDto(leg.Symbol, leg.Type, leg.Quantity, leg.Price, leg.Fee, leg.Currency,
                    pr.Legs.Count > i ? pr.Legs[i].Status.ToString().ToLowerInvariant() : "unselected")).ToList();
                var first = row.Legs[0];
                var canStake = row.Legs.Any(l => l.Type == "transfer_out" && (l.CanStake || accountIsCrypto));
                string status; string? reason = null;
                if (pr.Legs.Count == 0) status = "unselected";
                else if (pr.Legs.Any(l => l.Status == LegStatus.New)) status = "new";
                else if (pr.Legs.Any(l => l.Status == LegStatus.Update)) { status = "update"; reason = "Already imported; its timing changed (e.g. pending → confirmed), so it will be refreshed"; }
                else
                {
                    status = "duplicate";
                    reason = pr.Legs.Any(l => l.Status == LegStatus.Adopt)
                        ? "Already in this account from an earlier import; it will be linked to this row"
                        : "Already imported";
                }
                rows.Add(new PreviewRowDto(row.Row, status, reason, first.Date, first.OccurredAt, canStake && status == "new", legs));
            }

            var importable = file.Rows.Where(r => !r.Row.IsRejected).SelectMany(r => r.Row.Legs).ToList();
            var symbols = importable.Select(l => l.Symbol).Distinct().OrderBy(x => x, StringComparer.Ordinal);
            var assets = symbols.Select(sym => new AssetSummaryDto(
                sym,
                importable.Where(l => l.Symbol == sym && (l.Type is "transfer_in" or "buy")).Sum(l => l.Quantity),
                importable.Where(l => l.Symbol == sym && (l.Type is "transfer_out" or "sell")).Sum(l => l.Quantity),
                importable.Where(l => l.Symbol == sym && l.Type == "fee").Sum(l => l.Quantity),
                before.GetValueOrDefault(sym),
                after.GetValueOrDefault(sym))).ToList();

            var counts = importable.GroupBy(l => l.Type).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());
            var summary = new FileSummaryDto(
                rows.Count,
                rows.Count(r => r.Status == "new"),
                rows.Count(r => r.Status is "duplicate" or "update"),
                rows.Count(r => r.Status == "update"),
                rows.Count(r => r.Status == "rejected"),
                rows.Count(r => r.Status == "unselected"),
                counts, assets);
            result.Add(new FilePreviewDto(file.FileName, file.Parsed.Format, rows, summary));
        }
        return new ImportPreviewDto(result);
    }

    private static ImportFileRequest ToRequest(ImportFileInput f) => new(
        f.FileName, f.Content,
        f.Selection?.Rows is { } rows ? rows.ToHashSet() : null,
        f.Selection?.Staked is { } staked ? staked.ToHashSet() : null);

    /// <summary>A not-persisted transaction, used only to compute balances a plan would produce.</summary>
    private static Transaction Transient(int accountId, ImportLeg leg, bool staked) =>
        Transaction.Create(accountId, Symbol.Create(leg.Symbol), TransactionType.From(leg.Type), Quantity.Create(leg.Quantity),
            leg.Price, leg.Fee, CurrencyCode.Create(leg.Currency), TradeDate.Create(leg.Date), leg.Notes,
            isStaked: staked && leg.Type == "transfer_out", occurredAt: leg.OccurredAt, externalId: leg.ExternalId);

    private static Dictionary<string, decimal> Balances(IEnumerable<Transaction> txs) =>
        HoldingsCalculator.ForAccount(txs).ToDictionary(h => h.Symbol.Value, h => h.Quantity);
}
