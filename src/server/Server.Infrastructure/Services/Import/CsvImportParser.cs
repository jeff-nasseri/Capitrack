using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Server.Infrastructure.Services.Import;

/// <summary>One ledger movement produced by an import row; it becomes one Capitrack transaction.</summary>
/// <param name="Symbol">The asset symbol (e.g. BTC-USD).</param>
/// <param name="Type">buy / sell / transfer_in / transfer_out / dividend / interest / fee.</param>
/// <param name="Quantity">Units of the asset moved (always positive; the type gives the direction).</param>
/// <param name="Price">Unit price in <paramref name="Currency"/> at the time of the movement (0 when unknown).</param>
/// <param name="Fee">A monetary commission in <paramref name="Currency"/> (brokers); network fees are separate "fee" legs.</param>
/// <param name="Currency">The currency of <paramref name="Price"/> and <paramref name="Fee"/>.</param>
/// <param name="Date">The UTC calendar date (yyyy-MM-dd).</param>
/// <param name="Notes">A human-readable note.</param>
/// <param name="OccurredAt">The exact UTC instant, when known.</param>
/// <param name="ExternalId">The source's transaction id, when it has one.</param>
/// <param name="CanStake">Whether the user may flag this outflow as staked.</param>
/// <param name="Key">
/// The row's stable identity, built only from the source's immutable fields (never fiat values,
/// labels or the position in the file), suffixed with "#n" to count genuinely identical rows.
/// </param>
/// <param name="LocalDate">The date in the source's local time, when it can differ from <paramref name="Date"/>.</param>
/// <param name="NeedsPrice">The source gives no value for this leg: the importer prices it at the market price of its time.</param>
/// <param name="UnitFee">A fee the source states in the asset's own units; it becomes <paramref name="Fee"/> once the leg is priced.</param>
public sealed record ImportLeg(
    string Symbol, string Type, decimal Quantity, decimal Price, decimal Fee, string Currency,
    string Date, string Notes, DateTime? OccurredAt = null, string? ExternalId = null, bool CanStake = false,
    string Key = "", string? LocalDate = null, bool NeedsPrice = false, decimal UnitFee = 0);

/// <summary>The outcome of one CSV data row: the legs it produces, or why it produces none.</summary>
/// <param name="Row">The 1-based data row number within the file (for reporting only).</param>
/// <param name="Legs">The ledger legs (empty when rejected).</param>
/// <param name="Rejection">Why the row is not imported, or null when it is.</param>
public sealed record ParsedRow(int Row, IReadOnlyList<ImportLeg> Legs, string? Rejection)
{
    /// <summary>A row that is not imported, with the reason.</summary>
    public static ParsedRow Rejected(int row, string reason) => new(row, [], reason);

    /// <summary>True when the row produces no legs.</summary>
    public bool IsRejected => Rejection is not null;
}

/// <summary>A parsed import file: its format and every data row's outcome, in file order.</summary>
public sealed record ParsedFile(string Format, IReadOnlyList<string> Headers, IReadOnlyList<ParsedRow> Rows);

/// <summary>
/// Reads a CSV export, detects its format and turns every data row into ledger legs or a rejection
/// with a reason — never a silent drop. Parsing is pure (no database), so a preview and the import
/// that follows it see exactly the same result.
/// </summary>
public static class CsvImportParser
{
    /// <summary>The formats this parser understands.</summary>
    public static readonly IReadOnlyList<string> KnownFormats = ["revolut-stocks", "revolut-commodities", "trezor", "generic"];

    private static readonly HashSet<string> GenericTypes =
        ["buy", "sell", "transfer_in", "transfer_out", "dividend", "interest", "fee"];

    /// <summary>
    /// Native coins of account-model chains: a transaction moves the native coin out of the wallet
    /// at most once (its value). UTXO chains (BTC, LTC, DOGE, BCH, ADA …) can pay several outputs.
    /// </summary>
    private static readonly HashSet<string> AccountModelNatives = new(StringComparer.OrdinalIgnoreCase)
        { "ETH", "ETC", "BNB", "POL", "MATIC", "SOL", "XRP", "TRX", "AVAX", "ARB", "OP", "BASE" };

    /// <summary>Parses <paramref name="content"/>; <paramref name="formatHint"/> overrides detection.</summary>
    public static ParsedFile Parse(string content, string? formatHint = null)
    {
        var (records, headers) = ReadCsv(content);
        var format = !string.IsNullOrEmpty(formatHint) ? formatHint : DetectFormat(headers);
        if (records.Count == 0) return new ParsedFile(format, headers, []);

        var rows = format switch
        {
            "revolut-stocks" => RevolutStocks(records),
            "revolut-commodities" => RevolutCommodities(records),
            "trezor" => Trezor(records),
            "generic" => Generic(records),
            _ => records.Select((_, i) => ParsedRow.Rejected(i + 1, $"Unknown CSV format (headers: {string.Join(", ", headers)})")).ToList()
        };
        return new ParsedFile(format, headers, CountIdenticalRows(rows));
    }

    /// <summary>
    /// Appends "#n" to each leg's key base, counting legs whose immutable fields are identical (e.g.
    /// two identical trades on one day), so they are neither merged nor duplicated on re-import.
    /// </summary>
    private static List<ParsedRow> CountIdenticalRows(List<ParsedRow> rows)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        return rows.Select(row => row with
        {
            Legs = row.Legs.Select(leg =>
            {
                var n = seen.TryGetValue(leg.Key, out var c) ? c : 0;
                seen[leg.Key] = n + 1;
                return leg with { Key = $"{leg.Key}#{n}" };
            }).ToList()
        }).ToList();
    }

    /// <summary>A decimal without trailing zeros ("0.00100" and "0.001" are the same amount).</summary>
    private static string Norm(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    /// <summary>Identifies an export by its header row.</summary>
    public static string DetectFormat(IEnumerable<string> headers)
    {
        var h = headers.Select(s => s.ToLowerInvariant().Trim()).ToHashSet();
        if (h.Contains("ticker") && h.Contains("price per share")) return "revolut-stocks";
        if (h.Contains("product") && h.Contains("started date") && h.Contains("state")) return "revolut-commodities";
        if (h.Contains("transaction id") && h.Contains("amount unit")) return "trezor";
        if (h.Contains("symbol") && h.Contains("type")) return "generic";
        return "unknown";
    }

    /// <summary>Reads the CSV into header→value rows (case-insensitive headers).</summary>
    public static (List<Dictionary<string, string>> Records, List<string> Headers) ReadCsv(string content)
    {
        content = content.TrimStart('\uFEFF'); // a UTF-8 BOM would otherwise corrupt the first header name
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true, // European exports often use ';' (their decimal separator is ',')
            DetectDelimiterValues = [",", ";", "\t", "|"],
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
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
                dict[headers[i]] = (csv.TryGetField<string>(i, out var val) ? val : "")?.Trim() ?? "";
            records.Add(dict);
        }
        return (records, headers);
    }

    // ---------------------------------------------------------------- Trezor Suite

    /// <summary>
    /// Trezor Suite exports one row per transaction target/token movement; rows of one transaction
    /// share its id and the network fee is printed on the first row only. Per transaction:
    /// RECV → transfer_in; SENT → transfer_out; SELF/FAILED/CONTRACT move no value (a self-transfer
    /// returns to the wallet, a failed/contract call only burns gas); the fee is one "fee" leg in
    /// the fee's own asset (a token transfer's fee is paid in ETH), charged once per transaction.
    /// </summary>
    private static List<ParsedRow> Trezor(List<Dictionary<string, string>> records)
    {
        var sep = Separator(records, "Amount", "Fee", "Fiat (USD)");
        var result = new ParsedRow[records.Count];
        var numbered = records.Select((r, i) => (Row: i + 1, R: r)).ToList();

        // group rows by transaction; rows without an id stand alone
        foreach (var tx in numbered.GroupBy(x => Get(x.R, "Transaction ID") is { Length: > 0 } id ? id : $"\0row{x.Row}"))
        {
            var feeCharged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nativeDebited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // the fee-bearing row is the transaction's primary target, so it is classified first
            foreach (var (row, r) in tx.OrderBy(x => Get(x.R, "Fee").Length > 0 ? 0 : 1).ThenBy(x => x.Row))
                result[row - 1] = TrezorRow(row, r, sep, feeCharged, nativeDebited);
        }
        return [.. result];
    }

    private static ParsedRow TrezorRow(int row, Dictionary<string, string> r, DecimalSeparator sep,
                                       HashSet<string> feeCharged, HashSet<string> nativeDebited)
    {
        var kind = Get(r, "Type").ToUpperInvariant();
        var unit = Get(r, "Amount unit");
        var txId = Get(r, "Transaction ID");
        var when = ImportTimeParser.Trezor(Get(r, "Timestamp"), Get(r, "Date"), Get(r, "Time"));
        if (when is null) return ParsedRow.Rejected(row, "No valid timestamp or date");
        if (unit.Length == 0) return ParsedRow.Rejected(row, "No amount unit");

        switch (kind)
        {
            case "RECV" or "SENT" or "SELF" or "FAILED" or "CONTRACT": break;
            case "JOINT": return ParsedRow.Rejected(row, "Coinjoin (JOINT) transactions are not supported — add the net change manually");
            default: return ParsedRow.Rejected(row, $"Unsupported transaction type \"{Get(r, "Type")}\"");
        }

        var externalId = txId.Length > 0 ? txId : null;
        // the transaction's identity: its id, or (without one) the exact instant
        var txKey = txId.Length > 0 ? txId : $"at:{when.Utc?.Ticks.ToString(CultureInfo.InvariantCulture) ?? when.Date}";
        var address = Get(r, "Address");
        var fiat = DecimalParser.TryParse(Get(r, "Fiat (USD)"), sep, out var f) ? Math.Abs(f) : 0m;
        var legs = new List<ImportLeg>();
        decimal unitPrice = 0;

        if (kind is "RECV" or "SENT")
        {
            if (!DecimalParser.TryParse(Get(r, "Amount"), sep, out var amount))
                return ParsedRow.Rejected(row, $"Amount \"{Get(r, "Amount")}\" is not a number — an NFT/token id, which Capitrack does not track");
            amount = Math.Abs(amount);

            if (kind == "SENT" && AccountModelNatives.Contains(unit) && nativeDebited.Contains(unit))
                return ParsedRow.Rejected(row,
                    $"Internal contract transfer: this transaction already debited {unit} from the wallet; Trezor lists the contract's onward transfer as an extra row");
            if (kind == "SENT" && AccountModelNatives.Contains(unit)) nativeDebited.Add(unit);

            if (amount > 0)
            {
                unitPrice = fiat > 0 ? Math.Round(fiat / amount, 12) : 0;
                var direction = kind == "RECV" ? "in" : "out";
                legs.Add(new ImportLeg(CryptoSymbol(unit), kind == "RECV" ? "transfer_in" : "transfer_out", amount, unitPrice, 0, "USD",
                    when.Date, $"Trezor {kind}", when.Utc, externalId, CanStake: kind == "SENT",
                    // a transaction can pay several outputs of one asset: the address + amount tell them apart
                    Key: $"trezor|{txKey}|{direction}|{unit.ToUpperInvariant()}|{address}|{Norm(amount)}", LocalDate: when.LocalDate,
                    // an empty fiat column means Trezor had no rate; "0" means the amount is worth less than a cent
                    NeedsPrice: string.IsNullOrWhiteSpace(Get(r, "Fiat (USD)"))));
            }
        }
        else if (DecimalParser.TryParse(Get(r, "Amount"), sep, out var moved) && moved != 0 && fiat > 0)
        {
            unitPrice = Math.Round(fiat / Math.Abs(moved), 12); // SELF: the fiat column values the self-sent amount
        }

        // the network fee leaves the wallet only when this wallet sent the transaction; once per tx and asset
        if (kind != "RECV" && DecimalParser.TryParse(Get(r, "Fee"), sep, out var fee) && fee != 0)
        {
            var feeUnit = Get(r, "Fee unit") is { Length: > 0 } fu ? fu : unit;
            if (feeCharged.Add(feeUnit))
            {
                var feePrice = feeUnit.Equals(unit, StringComparison.OrdinalIgnoreCase) ? unitPrice : 0;
                legs.Add(new ImportLeg(CryptoSymbol(feeUnit), "fee", Math.Abs(fee), feePrice, 0, "USD",
                    when.Date, $"Network fee (Trezor {kind})", when.Utc, externalId,
                    Key: $"trezor|{txKey}|fee|{feeUnit.ToUpperInvariant()}", LocalDate: when.LocalDate));
            }
        }

        if (legs.Count == 0)
            return ParsedRow.Rejected(row, kind is "RECV" or "SENT"
                ? "Nothing to record: zero amount and no network fee"
                : $"Nothing to record: a {kind} transaction without a network fee");
        return new ParsedRow(row, legs, null);
    }

    /// <summary>Capitrack prices crypto against USD: BTC → BTC-USD.</summary>
    private static string CryptoSymbol(string unit) => $"{unit.Trim().ToUpperInvariant()}-USD";

    // ---------------------------------------------------------------- Revolut stocks

    private static List<ParsedRow> RevolutStocks(List<Dictionary<string, string>> records)
    {
        var sep = Separator(records, "Quantity", "Price per share", "Total Amount", "FX Rate");
        var rows = new List<ParsedRow>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var row = i + 1;
            var r = records[i];
            var type = Get(r, "Type");
            var ticker = Get(r, "Ticker").ToUpperInvariant();

            var txType = type switch
            {
                "BUY - MARKET" or "BUY - LIMIT" => "buy",
                "SELL - MARKET" or "SELL - LIMIT" => "sell",
                "DIVIDEND" => "dividend",
                "STOCK SPLIT" => "transfer_in",
                _ => ""
            };
            if (type is "CASH TOP-UP" or "CASH WITHDRAWAL") { rows.Add(ParsedRow.Rejected(row, $"Cash movement ({type}), not a position")); continue; }
            if (txType == "") { rows.Add(ParsedRow.Rejected(row, $"Unsupported Revolut type \"{type}\"")); continue; }
            if (ticker.Length == 0) { rows.Add(ParsedRow.Rejected(row, $"{type} without a ticker")); continue; }

            var when = ImportTimeParser.Iso(Get(r, "Date"));
            if (when is null) { rows.Add(ParsedRow.Rejected(row, $"Invalid date \"{Get(r, "Date")}\"")); continue; }

            var quantity = Math.Abs(Num(Get(r, "Quantity"), sep));
            var price = Num(Get(r, "Price per share"), sep);
            var total = Math.Abs(Num(Get(r, "Total Amount"), sep));
            var currency = Get(r, "Currency") is { Length: > 0 } c ? c : "USD";

            decimal finalQty = quantity, finalPrice = price;
            if (txType == "dividend") { finalQty = total; finalPrice = 1; }
            if (txType == "transfer_in" && quantity == 0 && total == 0) { finalQty = Num(Get(r, "Quantity"), sep); finalPrice = 0; }
            if (finalQty == 0) { rows.Add(ParsedRow.Rejected(row, $"{type} with zero quantity")); continue; }

            rows.Add(new ParsedRow(row, [new ImportLeg(ticker, txType, finalQty, finalPrice, 0, currency, when.Date, $"Revolut: {type}", when.Utc,
                Key: $"revolut-stocks|{Get(r, "Date")}|{ticker}|{type}|{Norm(finalQty)}|{currency}")], null));
        }
        return rows;
    }

    // ---------------------------------------------------------------- Revolut commodities

    private static readonly Dictionary<string, string> MetalSymbols = new() { ["XAU"] = "GC=F", ["XAG"] = "SI=F", ["XPT"] = "PL=F", ["XPD"] = "PA=F" };

    private static List<ParsedRow> RevolutCommodities(List<Dictionary<string, string>> records)
    {
        var sep = Separator(records, "Amount", "Fee", "Balance");
        var rows = new List<ParsedRow>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var row = i + 1;
            var r = records[i];
            var state = Get(r, "State");
            if (state != "COMPLETED") { rows.Add(ParsedRow.Rejected(row, $"Not completed ({state}) — import it again once it completes")); continue; }

            var description = Get(r, "Description");
            string txType;
            if (description.Contains("Exchanged to EUR") || description.Contains("Exchanged to USD")) txType = "sell";
            else if (description.StartsWith("Exchanged to")) txType = "buy";
            else { rows.Add(ParsedRow.Rejected(row, $"Not a buy/sell exchange (\"{description}\")")); continue; }

            var dateStr = Get(r, "Started Date") is { Length: > 0 } s ? s : Get(r, "Completed Date");
            var when = ImportTimeParser.Iso(dateStr); // Revolut gives no zone: taken as UTC
            if (when is null) { rows.Add(ParsedRow.Rejected(row, $"Invalid date \"{dateStr}\"")); continue; }

            var amount = Math.Abs(Num(Get(r, "Amount"), sep));
            if (amount == 0) { rows.Add(ParsedRow.Rejected(row, "Zero amount")); continue; }
            var fee = Math.Abs(Num(Get(r, "Fee"), sep));
            var currency = Get(r, "Currency") is { Length: > 0 } c ? c : "XAU";
            var symbol = MetalSymbols.GetValueOrDefault(currency, currency);

            // the statement gives only the metal amount (and the fee in metal units): priced at the market price of its time
            var feeNote = fee > 0 ? $"; fee {Norm(fee)} {currency}" : "";
            rows.Add(new ParsedRow(row, [new ImportLeg(symbol, txType, amount, 0, 0, "EUR", when.Date,
                $"Revolut Commodity: {description} ({currency}){feeNote}", when.Utc,
                Key: $"revolut-commodities|{dateStr}|{description}|{Norm(amount)}|{currency}", NeedsPrice: true, UnitFee: fee)], null));
        }
        return rows;
    }

    // ---------------------------------------------------------------- generic

    private static List<ParsedRow> Generic(List<Dictionary<string, string>> records)
    {
        var sep = Separator(records, "quantity", "price", "fee");
        var rows = new List<ParsedRow>(records.Count);
        for (var i = 0; i < records.Count; i++)
        {
            var row = i + 1;
            var r = records[i];
            var symbol = Get(r, "symbol").ToUpperInvariant();
            var type = (Get(r, "type") is { Length: > 0 } t ? t : "buy").ToLowerInvariant();
            var rawDate = Get(r, "date");
            if (symbol.Length == 0) { rows.Add(ParsedRow.Rejected(row, "Missing symbol")); continue; }
            if (!GenericTypes.Contains(type)) { rows.Add(ParsedRow.Rejected(row, $"Unsupported type \"{type}\"")); continue; }
            var when = ImportTimeParser.Iso(rawDate);
            var date = when?.Date ?? (DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null);
            if (date is null) { rows.Add(ParsedRow.Rejected(row, $"Invalid date \"{rawDate}\"")); continue; }

            var quantity = Num(Get(r, "quantity"), sep);
            if (quantity < 0) { rows.Add(ParsedRow.Rejected(row, "Negative quantity")); continue; }
            var currency = Get(r, "currency") is { Length: > 0 } c ? c.ToUpperInvariant() : "EUR";
            var id = Get(r, "id") is { Length: > 0 } i2 ? i2 : null;
            rows.Add(new ParsedRow(row, [new ImportLeg(symbol, type, quantity, Num(Get(r, "price"), sep), Num(Get(r, "fee"), sep),
                currency, date, Get(r, "notes"), when?.Utc, id,
                // with an id column the id is the identity; otherwise the immutable trade fields (not price/fee/notes)
                Key: id is not null ? $"generic|id|{id}" : $"generic|{symbol}|{type}|{date}|{Norm(quantity)}|{currency}")], null));
        }
        return rows;
    }

    // ---------------------------------------------------------------- helpers

    private static string Get(Dictionary<string, string> r, string key) =>
        r.TryGetValue(key, out var v) ? v.Trim() : "";

    private static decimal Num(string s, DecimalSeparator sep) => DecimalParser.TryParse(s, sep, out var d) ? d : 0;

    /// <summary>The file's decimal separator, inferred once from all values of its numeric columns.</summary>
    private static DecimalSeparator Separator(List<Dictionary<string, string>> records, params string[] columns) =>
        DecimalParser.Detect(records.SelectMany(r => columns.Select(c => r.TryGetValue(c, out var v) ? v : null)));
}
