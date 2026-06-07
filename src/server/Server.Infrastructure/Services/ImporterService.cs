using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services;

/// <summary>Port of services/importer.ts — format detection, four parsers, fingerprint dedup.</summary>
public sealed partial class ImporterService(CapitrackDbContext db) : IImporterService
{
    private static readonly string[] ValidTypes =
        ["buy", "sell", "transfer_in", "transfer_out", "dividend", "interest", "fee"];

    private record ImportedTransaction(string Symbol, string Type, double Quantity, double Price, double Fee, string Currency, string Date, string Notes);

    // ---- CSV parsing into header->value dictionaries (csv-parse columns:true equivalent) ----
    private static (List<Dictionary<string, string>> Records, List<string> Headers) ParseCsv(string content)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            BadDataFound = null,
            HeaderValidated = null,
            IgnoreBlankLines = true,
            DetectColumnCountChanges = false
        };
        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, config);
        var records = new List<Dictionary<string, string>>();
        if (!csv.Read()) return (records, []);
        csv.ReadHeader();
        var headers = (csv.HeaderRecord ?? []).Select(h => h.Trim()).ToList();
        while (csv.Read())
        {
            var dict = new Dictionary<string, string>();
            for (var i = 0; i < headers.Count; i++)
                dict[headers[i]] = (csv.TryGetField<string>(i, out var val) ? val : "")?.Trim() ?? "";
            records.Add(dict);
        }
        return (records, headers);
    }

    // ---- Format detection ----
    private static string DetectFormat(IEnumerable<string> headers)
    {
        var h = headers.Select(s => s.ToLowerInvariant().Trim()).ToHashSet();
        if (h.Contains("ticker") && h.Contains("price per share")) return "revolut-stocks";
        if (h.Contains("product") && h.Contains("started date") && h.Contains("state")) return "revolut-commodities";
        if (h.Contains("transaction id") && h.Contains("amount unit")) return "trezor";
        if (h.Contains("symbol") && h.Contains("type")) return "generic";
        return "unknown";
    }

    public DetectResultDto Detect(string content)
    {
        var (records, headers) = ParseCsv(content);
        if (records.Count == 0) return new DetectResultDto("unknown", []);
        return new DetectResultDto(DetectFormat(headers), headers);
    }

    // ---- Fingerprint / dedup ----
    private static string Fingerprint(int accountId, string symbol, string type, double quantity, double price, string date)
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

    private static readonly string[] KnownFormats = ["revolut-stocks", "revolut-commodities", "trezor", "generic"];

    // ---- Shared parse (format detection + parser dispatch) ----
    private static (string Format, List<ImportedTransaction> Parsed, List<string> Headers) ParseContent(string content, string? formatHint)
    {
        var (records, headers) = ParseCsv(content);
        if (records.Count == 0) return ("unknown", [], headers);

        var format = !string.IsNullOrEmpty(formatHint) ? formatHint : DetectFormat(headers);
        List<ImportedTransaction> parsed = format switch
        {
            "revolut-stocks" => ParseRevolutStock(records),
            "revolut-commodities" => ParseRevolutCommodity(records),
            "trezor" => ParseTrezor(records),
            "generic" => ParseGeneric(records),
            _ => []
        };
        return (format, parsed, headers);
    }

    // ---- Main import ----
    public async Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint)
    {
        var (records, _) = ParseCsv(content);
        if (records.Count == 0) return new ImportResultDto(0, 0, 0, [], "unknown");

        var (format, parsed, headers) = ParseContent(content, formatHint);
        if (!KnownFormats.Contains(format))
            return new ImportResultDto(0, 0, records.Count, [$"Unknown CSV format. Headers: {string.Join(", ", headers)}"], "unknown");

        var existing = await ExistingFingerprintsAsync(accountId);
        int imported = 0, skipped = 0;
        var errors = new List<string>();
        for (var i = 0; i < parsed.Count; i++)
        {
            var tx = parsed[i];
            try
            {
                var fp = Fingerprint(accountId, tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Date);
                if (existing.Contains(fp)) { skipped++; continue; }
                db.Transactions.Add(Transaction.Create(
                    accountId,
                    Symbol.Create(tx.Symbol),
                    TransactionType.From(tx.Type),
                    Quantity.Create(tx.Quantity),
                    tx.Price,
                    tx.Fee,
                    CurrencyCode.Create(tx.Currency),
                    TradeDate.Create(tx.Date),
                    tx.Notes));
                existing.Add(fp);
                imported++;
            }
            catch (Exception e) { errors.Add($"Row {i + 1}: {e.Message}"); }
        }
        await db.SaveChangesAsync();
        return new ImportResultDto(imported, skipped, parsed.Count, errors, format);
    }

    // ---- Preview (parse only, no insert) ----
    public async Task<PreviewFileDto> PreviewAsync(string fileName, string content, int accountId)
    {
        var (format, parsed, _) = ParseContent(content, null);
        var existing = await ExistingFingerprintsAsync(accountId);

        var account = await db.Accounts.FindAsync(accountId);
        bool accountIsCrypto = account?.Type.Value is "crypto";

        var rows = new List<PreviewTransactionDto>(parsed.Count);
        for (var i = 0; i < parsed.Count; i++)
        {
            var tx = parsed[i];
            var fp = Fingerprint(accountId, tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Date);
            bool symbolIsCrypto = tx.Symbol.EndsWith("-USD", StringComparison.OrdinalIgnoreCase);
            bool canStake = tx.Type == "transfer_out" && (symbolIsCrypto || accountIsCrypto);

            rows.Add(new PreviewTransactionDto(
                i, tx.Symbol, tx.Type, tx.Quantity, tx.Price, tx.Fee,
                tx.Currency, tx.Date, tx.Notes, existing.Contains(fp), canStake));
        }
        return new PreviewFileDto(fileName, format, rows);
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
                db.Transactions.Add(Transaction.Create(
                    accountId,
                    Symbol.Create(tx.Symbol),
                    TransactionType.From(tx.Type),
                    Quantity.Create(tx.Quantity),
                    tx.Price,
                    tx.Fee,
                    CurrencyCode.Create(tx.Currency),
                    TradeDate.Create(tx.Date),
                    tx.Notes,
                    isStaked: tx.IsStaked));
                existing.Add(fp);
                imported++;
            }
            catch (Exception e) { errors.Add($"Row {i + 1}: {e.Message}"); }
        }
        await db.SaveChangesAsync();
        return new ImportResultDto(imported, skipped, list.Count, errors, "selected");
    }

    // ---- Parsers ----
    private static List<ImportedTransaction> ParseRevolutStock(List<Dictionary<string, string>> records)
    {
        var list = new List<ImportedTransaction>();
        foreach (var r in records)
        {
            var type = Get(r, "Type").Trim();
            var ticker = Get(r, "Ticker").Trim();
            if (string.IsNullOrEmpty(ticker) || type is "CASH TOP-UP" or "CASH WITHDRAWAL") continue;

            string txType = type switch
            {
                "BUY - MARKET" => "buy",
                "SELL - MARKET" => "sell",
                "DIVIDEND" => "dividend",
                "STOCK SPLIT" => "transfer_in",
                _ => ""
            };
            if (txType == "") continue;

            double quantity = Math.Abs(Num(Get(r, "Quantity", "0")));
            double price = Num(Clean(Get(r, "Price per share", "0")));
            double total = Math.Abs(Num(Clean(Get(r, "Total Amount", "0"))));
            var currency = Get(r, "Currency", "USD").Trim();
            var date = IsoDate(Get(r, "Date").Trim());

            double finalQty = quantity, finalPrice = price;
            if (txType == "dividend") { finalQty = total; finalPrice = 1; }
            if (txType == "transfer_in" && quantity == 0 && total == 0) { finalQty = Num(Get(r, "Quantity", "0")); finalPrice = 0; }
            if (string.IsNullOrEmpty(date)) continue;

            list.Add(new ImportedTransaction(ticker.ToUpperInvariant(), txType, finalQty, finalPrice, 0, currency, date, $"Revolut: {type}"));
        }
        return list;
    }

    private static List<ImportedTransaction> ParseRevolutCommodity(List<Dictionary<string, string>> records)
    {
        var list = new List<ImportedTransaction>();
        var symbolMap = new Dictionary<string, string> { ["XAU"] = "GC=F", ["XAG"] = "SI=F", ["XPT"] = "PL=F", ["XPD"] = "PA=F" };
        foreach (var r in records)
        {
            if (Get(r, "State").Trim() != "COMPLETED") continue;
            var description = Get(r, "Description").Trim();
            double amount = Num(Get(r, "Amount", "0"));
            double fee = Math.Abs(Num(Get(r, "Fee", "0")));
            var currency = Get(r, "Currency", "XAU").Trim();
            var dateStr = Get(r, "Started Date", Get(r, "Completed Date")).Trim();
            var date = string.IsNullOrEmpty(dateStr) ? "" : dateStr.Split(' ')[0];
            if (string.IsNullOrEmpty(date)) continue;

            var symbol = symbolMap.GetValueOrDefault(currency, currency);
            string txType;
            if (description.Contains("Exchanged to EUR") || description.Contains("Exchanged to USD")) txType = "sell";
            else if (description.StartsWith("Exchanged to")) txType = "buy";
            else continue;

            list.Add(new ImportedTransaction(symbol, txType, Math.Abs(amount), 0, fee, "EUR", date, $"Revolut Commodity: {description} ({currency})"));
        }
        return list;
    }

    private static List<ImportedTransaction> ParseTrezor(List<Dictionary<string, string>> records)
    {
        var list = new List<ImportedTransaction>();
        var symbolMap = new Dictionary<string, string> { ["BTC"] = "BTC-USD", ["ETH"] = "ETH-USD", ["LTC"] = "LTC-USD" };
        foreach (var r in records)
        {
            var type = Get(r, "Type").Trim().ToUpperInvariant();
            double amount = Math.Abs(Num(Get(r, "Amount", "0")));
            var amountUnit = Get(r, "Amount unit", "BTC").Trim();
            double fiatUsd = Math.Abs(Num(Clean(Get(r, "Fiat (USD)", "0"))));
            double fee = Math.Abs(Num(Clean(Get(r, "Fee", "0"))));
            var dateStr = Get(r, "Date").Trim();
            var txId = Get(r, "Transaction ID").Trim();

            string date = "";
            if (!string.IsNullOrEmpty(dateStr))
            {
                var parts = dateStr.Split('/');
                if (parts.Length == 3)
                    date = $"{parts[2]}-{parts[0].PadLeft(2, '0')}-{parts[1].PadLeft(2, '0')}";
            }
            if (string.IsNullOrEmpty(date) || amount == 0) continue;

            var symbol = symbolMap.GetValueOrDefault(amountUnit, $"{amountUnit}-USD");
            string txType = type switch { "RECV" => "transfer_in", "SENT" => "transfer_out", _ => "" };
            if (txType == "") continue;

            double price = amount > 0 ? fiatUsd / amount : 0;
            var notes = !string.IsNullOrEmpty(txId) ? $"TxID: {txId[..Math.Min(16, txId.Length)]}..." : $"Trezor {amountUnit}";
            list.Add(new ImportedTransaction(symbol, txType, amount, price, fee, "USD", date, notes));
        }
        return list;
    }

    private static List<ImportedTransaction> ParseGeneric(List<Dictionary<string, string>> records)
    {
        var list = new List<ImportedTransaction>();
        foreach (var r in records)
        {
            var symbol = Pick(r, "symbol", "Symbol", "SYMBOL").ToUpperInvariant();
            var type = Or(Pick(r, "type", "Type", "TYPE"), "buy").ToLowerInvariant();
            double quantity = Num(Or(Pick(r, "quantity", "Quantity", "QUANTITY"), "0"));
            double price = Num(Or(Pick(r, "price", "Price", "PRICE"), "0"));
            double fee = Num(Or(Pick(r, "fee", "Fee", "FEE"), "0"));
            var currency = Or(Pick(r, "currency", "Currency", "CURRENCY"), "EUR");
            var date = Pick(r, "date", "Date", "DATE");
            var notes = Pick(r, "notes", "Notes", "NOTES");
            if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(date)) continue;
            if (!ValidTypes.Contains(type)) continue;
            list.Add(new ImportedTransaction(symbol, type, quantity, price, fee, currency, date, notes));
        }
        return list;
    }

    // ---- Helpers ----
    private static string Get(Dictionary<string, string> r, string key, string fallback = "") =>
        r.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;

    // First non-empty value among the given keys (or "").
    private static string Pick(Dictionary<string, string> r, params string[] keys)
    {
        foreach (var k in keys)
            if (r.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v)) return v;
        return "";
    }

    private static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string Clean(string s) => MyRegex().Replace(s, "");
    private static double Num(string s) => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static string IsoDate(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto)
            ? dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
    }

    [GeneratedRegex("[^0-9.\\-]")]
    private static partial Regex MyRegex();
}
