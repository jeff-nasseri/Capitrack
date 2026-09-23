using System.Globalization;
using System.Text.Json;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// Twelve Data (US stocks/ETFs, crypto, forex, gold/silver). Needs a free key in
/// <c>TWELVE_DATA_API_KEY</c>; the free Basic plan allows 800 credits/day and 8 requests/minute and
/// covers US listings, forex and crypto (not other exchanges' suffixed tickers such as VWCE.DE).
/// </summary>
public sealed class TwelveDataProvider(HttpClient http, string? apiKey, TimeSpan? rateLimit = null) : IPriceProvider
{
    private readonly string? _key = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
    private readonly RateGate _gate = new(rateLimit ?? TimeSpan.FromSeconds(7.6)); // 8 requests/minute

    private static readonly Dictionary<string, string> MetalCodes = new(StringComparer.OrdinalIgnoreCase) { ["XAU"] = "XAU", ["XAG"] = "XAG" };

    public ProviderInfo Info { get; } = new(
        "twelvedata", "Twelve Data", [AssetClass.Stock, AssetClass.Crypto, AssetClass.Metal, AssetClass.Fx], RequiresApiKey: true,
        ApiKeyEnvVar: "TWELVE_DATA_API_KEY", ApiKeyUrl: "https://twelvedata.com/account/api-keys",
        Website: "https://twelvedata.com",
        Limits: "Free Basic plan with a key: 800 credits/day, 8 requests/minute; US stocks/ETFs, forex and crypto.");

    public bool IsConfigured => _key is not null;

    public bool Supports(string symbol, AssetClass assetClass) => TdSymbol(symbol, assetClass) is not null;

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var td = TdSymbol(symbol, MarketSymbols.Classify(symbol));
        if (td is null || _key is null) return null;
        using var doc = await GetAsync($"time_series?symbol={Uri.EscapeDataString(td)}&interval=1day&start_date={Iso(from)}&end_date={Iso(to.AddDays(1))}&order=ASC&outputsize=5000", ct);
        if (doc is null || !doc.RootElement.TryGetProperty("values", out var values)) return null;

        var closes = new List<DailyClose>();
        foreach (var v in values.EnumerateArray())
            if (DateOnly.TryParseExact(v.GetProperty("datetime").GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && JsonNumbers.ToDecimal(v.GetProperty("close")) is { } close && d >= from && d <= to)
                closes.Add(new DailyClose(d, close));
        return closes.Count == 0 ? null : new PriceSeries(symbol, Currency(doc.RootElement, symbol), Info.Id, closes.OrderBy(c => c.Date).ToList());
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        var td = TdSymbol(symbol, MarketSymbols.Classify(symbol));
        if (td is null || _key is null) return null;
        using var doc = await GetAsync($"quote?symbol={Uri.EscapeDataString(td)}", ct);
        if (doc is null || !doc.RootElement.TryGetProperty("close", out var close)) return null;
        var change = doc.RootElement.TryGetProperty("percent_change", out var pc) && JsonNumbers.ToDecimal(pc) is { } p ? (double)p : 0;
        return new QuoteDto
        {
            Symbol = symbol, Price = JsonNumbers.ToDecimal(close) ?? 0, Currency = Currency(doc.RootElement, symbol),
            Name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? symbol : symbol, ChangePercent = change
        };
    }

    /// <summary>How Twelve Data names the instrument, or null when the free plan cannot price it.</summary>
    private static string? TdSymbol(string symbol, AssetClass assetClass) => assetClass switch
    {
        AssetClass.Stock when !symbol.Contains('.') && !symbol.Contains('=') => symbol.ToUpperInvariant(),
        AssetClass.Crypto when MarketSymbols.CryptoPair(symbol) is { } c && !c.Base.Contains('-') => $"{c.Base}/{c.Quote}",
        AssetClass.Metal when MarketSymbols.MetalCode(symbol) is { } m && MetalCodes.ContainsKey(m) => $"{m}/USD",
        AssetClass.Fx when MarketSymbols.FxPair(symbol) is { } f => $"{f.Base}/{f.Quote}",
        _ => null
    };

    private static string Currency(JsonElement root, string symbol)
    {
        var meta = root.TryGetProperty("meta", out var m) ? m : root;
        foreach (var name in new[] { "currency", "currency_quote" })
            if (meta.TryGetProperty(name, out var c) && c.GetString() is { Length: 3 } code) return code.ToUpperInvariant();
        return MarketSymbols.CryptoPair(symbol)?.Quote ?? MarketSymbols.FxPair(symbol)?.Quote ?? "USD";
    }

    /// <summary>The response, or null for "no data" answers; throws on quota/transport errors.</summary>
    private async Task<JsonDocument?> GetAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        using var response = await http.GetAsync($"https://api.twelvedata.com/{path}&apikey={Uri.EscapeDataString(_key!)}", ct);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "error")
        {
            var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            var message = doc.RootElement.TryGetProperty("message", out var msg) ? msg.GetString() : "";
            doc.Dispose();
            if (code is 400 or 404) return null; // unknown symbol / no data for the range
            throw new HttpRequestException($"Twelve Data {code}: {message}");
        }
        return doc;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
