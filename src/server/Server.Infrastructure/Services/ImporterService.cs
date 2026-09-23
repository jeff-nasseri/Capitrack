using System.Globalization;
using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;
using Server.Infrastructure.Services.Import;

namespace Server.Infrastructure.Services;

/// <summary>
/// CSV import: format detection and parsing are delegated to <see cref="CsvImportParser"/> (every row
/// becomes ledger legs or a rejection with a reason); this service checks legs against the account's
/// existing transactions and writes them.
/// </summary>
public sealed class ImporterService(CapitrackDbContext db) : IImporterService
{
    public DetectResultDto Detect(string content)
    {
        var (records, headers) = CsvImportParser.ReadCsv(content);
        if (records.Count == 0) return new DetectResultDto("unknown", []);
        return new DetectResultDto(CsvImportParser.DetectFormat(headers), headers);
    }

    // ---- Fingerprint / dedup ----
    private static string Fingerprint(int accountId, string symbol, string type, decimal quantity, decimal price, string date)
    {
        var datePart = (date ?? "").Split('T')[0].Split(' ')[0];
        return $"{accountId}|{symbol}|{type}|{quantity.ToString("F8", CultureInfo.InvariantCulture)}|{price.ToString("F4", CultureInfo.InvariantCulture)}|{datePart}";
    }

    private async Task<HashSet<string>> ExistingFingerprintsAsync(int accountId)
    {
        var set = new HashSet<string>();
        foreach (var t in await db.Transactions.Where(t => t.AccountId == accountId).ToListAsync())
            set.Add(Fingerprint(t.AccountId, t.Symbol.Value, t.Type.Value, t.Quantity.Value, t.Price, t.Date.Value));
        return set;
    }

    // ---- Main import ----
    public async Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint)
    {
        var file = CsvImportParser.Parse(content, formatHint);
        if (file.Rows.Count == 0) return new ImportResultDto(0, 0, 0, [], file.Format);
        if (!CsvImportParser.KnownFormats.Contains(file.Format))
            return new ImportResultDto(0, 0, file.Rows.Count, [$"Unknown CSV format. Headers: {string.Join(", ", file.Headers)}"], "unknown");

        var existing = await ExistingFingerprintsAsync(accountId);
        int imported = 0, skipped = 0;
        var errors = new List<string>();
        foreach (var row in file.Rows.Where(r => !r.IsRejected))
        {
            foreach (var leg in row.Legs)
            {
                try
                {
                    var fp = Fingerprint(accountId, leg.Symbol, leg.Type, leg.Quantity, leg.Price, leg.Date);
                    if (existing.Contains(fp)) { skipped++; continue; }
                    db.Transactions.Add(ToTransaction(accountId, leg, isStaked: false));
                    existing.Add(fp);
                    imported++;
                }
                catch (Exception e) { errors.Add($"Row {row.Row}: {e.Message}"); }
            }
        }
        await db.SaveChangesAsync();
        var rejections = file.Rows.Where(r => r.IsRejected).Select(r => $"Row {r.Row}: {r.Rejection}").ToList();
        return new ImportResultDto(imported, skipped, file.Rows.Count, errors, file.Format, rejections.Count, rejections);
    }

    // ---- Preview (parse only, no insert) ----
    public async Task<PreviewFileDto> PreviewAsync(string fileName, string content, int accountId)
    {
        var file = CsvImportParser.Parse(content);
        var existing = await ExistingFingerprintsAsync(accountId);

        var account = await db.Accounts.FindAsync(accountId);
        var accountIsCrypto = account?.Type.Value is "crypto";

        var legs = new List<PreviewTransactionDto>();
        foreach (var row in file.Rows.Where(r => !r.IsRejected))
            foreach (var leg in row.Legs)
            {
                var fp = Fingerprint(accountId, leg.Symbol, leg.Type, leg.Quantity, leg.Price, leg.Date);
                var canStake = leg.Type == "transfer_out" && (leg.CanStake || accountIsCrypto);
                legs.Add(new PreviewTransactionDto(
                    legs.Count, leg.Symbol, leg.Type, leg.Quantity, leg.Price, leg.Fee,
                    leg.Currency, leg.Date, leg.Notes, existing.Contains(fp), canStake, leg.OccurredAt, leg.ExternalId));
            }
        var rejected = file.Rows.Where(r => r.IsRejected).Select(r => new RejectedRowDto(r.Row, r.Rejection!)).ToList();
        return new PreviewFileDto(fileName, file.Format, legs, rejected);
    }

    // ---- Import a user-selected set (no CSV parsing) ----
    public async Task<ImportResultDto> ImportSelectedAsync(int accountId, IEnumerable<SelectedTransactionDto> transactions)
    {
        var list = transactions as IReadOnlyList<SelectedTransactionDto> ?? transactions.ToList();
        var existing = await ExistingFingerprintsAsync(accountId);
        int imported = 0, skipped = 0;
        var errors = new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var tx = list[i];
            try
            {
                var fp = Fingerprint(accountId, tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Date);
                if (existing.Contains(fp)) { skipped++; continue; }
                db.Transactions.Add(ToTransaction(accountId,
                    new ImportLeg(tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Fee, tx.Currency, tx.Date, tx.Notes ?? "", tx.OccurredAt, tx.ExternalId),
                    tx.IsStaked));
                existing.Add(fp);
                imported++;
            }
            catch (Exception e) { errors.Add($"Row {i + 1}: {e.Message}"); }
        }
        await db.SaveChangesAsync();
        return new ImportResultDto(imported, skipped, list.Count, errors, "selected");
    }

    private static Transaction ToTransaction(int accountId, ImportLeg leg, bool isStaked) =>
        Transaction.Create(
            accountId,
            Symbol.Create(leg.Symbol),
            TransactionType.From(leg.Type),
            Quantity.Create(leg.Quantity),
            leg.Price,
            leg.Fee,
            CurrencyCode.Create(leg.Currency),
            TradeDate.Create(leg.Date),
            leg.Notes,
            isStaked: isStaked && leg.Type == "transfer_out",
            occurredAt: leg.OccurredAt,
            externalId: leg.ExternalId);
}
