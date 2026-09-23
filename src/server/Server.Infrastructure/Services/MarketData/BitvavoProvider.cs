using System.Text.Json;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// Bitvavo public market data (crypto, EUR markets). No key, full history, including hourly candles
/// for any past date. Prices are in EUR whatever the symbol's quote currency (BTC-USD → BTC-EUR);
/// the series says so, and valuation converts it like any other currency.
/// </summary>
public sealed class BitvavoProvider(HttpClient http, TimeSpan? rateLimit = null) : IPriceProvider
{
    private readonly RateGate _gate = new(rateLimit ?? TimeSpan.FromMilliseconds(150)); // 1,000 weight/min; a candle request weighs 1

    public ProviderInfo Info { get; } = new(
        "bitvavo", "Bitvavo", [AssetClass.Crypto], RequiresApiKey: false, ApiKeyEnvVar: null, ApiKeyUrl: null,
        Website: "https://docs.bitvavo.com/",
        Limits: "Free, no key. 1,000 request-weight/min. Full daily and hourly history; prices in EUR.");

    public bool IsConfigured => true;

    public bool Supports(string symbol, AssetClass assetClass) =>
        assetClass == AssetClass.Crypto && MarketSymbols.CryptoPair(symbol) is { } p && !p.Base.Contains('-');

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var closes = new List<DailyClose>();
        // at most 1,440 candles per request
        for (var start = from; start <= to; start = start.AddDays(1000))
        {
            var end = start.AddDays(999) < to ? start.AddDays(999) : to;
            foreach (var c in await CandlesAsync(Market(symbol), "1d", Ms(start), Ms(end.AddDays(1)) - 1, ct))
                closes.Add(new DailyClose(DateOnly.FromDateTime(c.Open), c.Close)); // a daily candle opens at 00:00 UTC
        }
        closes = closes.Where(c => c.Date >= from && c.Date <= to).DistinctBy(c => c.Date).OrderBy(c => c.Date).ToList();
        return closes.Count == 0 ? null : new PriceSeries(symbol, "EUR", Info.Id, closes);
    }

    public async Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto)) return null;
        var candles = await CandlesAsync(Market(symbol), "1h", Ms(utc.AddHours(-2)), Ms(utc.AddHours(2)), ct);
        var c = candles.FirstOrDefault(x => x.Open <= utc && utc < x.Open.AddHours(1));
        return c is null ? null : new PricePoint(c.Close, "EUR", Info.Id, "hour", c.Open);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto)) return null;
        using var doc = await GetAsync($"ticker/24h?market={Market(symbol)}", ct);
        return doc is null ? null : ToQuote(symbol, doc.RootElement);
    }

    /// <summary>Every market's 24-hour ticker comes in one request.</summary>
    public async Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        var quotes = new Dictionary<string, QuoteDto>();
        var wanted = symbols.Where(s => Supports(s, AssetClass.Crypto)).ToList();
        if (wanted.Count == 0) return quotes;
        using var doc = await GetAsync("ticker/24h", ct);
        if (doc is null) return quotes;
        var byMarket = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in doc.RootElement.EnumerateArray())
            if (t.TryGetProperty("market", out var m) && m.GetString() is { } market) byMarket[market] = t;
        foreach (var symbol in wanted)
            if (byMarket.TryGetValue(Market(symbol), out var t) && ToQuote(symbol, t) is { } quote) quotes[symbol] = quote;
        return quotes;
    }

    private static QuoteDto? ToQuote(string symbol, JsonElement ticker)
    {
        var last = ticker.TryGetProperty("last", out var l) ? JsonNumbers.ToDecimal(l) ?? 0 : 0;
        if (last <= 0) return null;
        var open = ticker.TryGetProperty("open", out var o) ? JsonNumbers.ToDecimal(o) ?? 0 : 0;
        return new QuoteDto { Symbol = symbol, Price = last, Currency = "EUR", Name = symbol, ChangePercent = open > 0 ? (double)((last - open) / open * 100) : 0 };
    }

    private sealed record Candle(DateTime Open, decimal Close);

    private async Task<List<Candle>> CandlesAsync(string market, string interval, long start, long end, CancellationToken ct)
    {
        using var doc = await GetAsync($"{market}/candles?interval={interval}&start={start}&end={end}&limit=1440", ct);
        var list = new List<Candle>();
        if (doc is null) return list;
        foreach (var r in doc.RootElement.EnumerateArray()) // [timestamp ms, open, high, low, close, volume], newest first
            if (JsonNumbers.ToDecimal(r[4]) is { } close)
                list.Add(new Candle(DateTimeOffset.FromUnixTimeMilliseconds(r[0].GetInt64()).UtcDateTime, close));
        return list;
    }

    /// <summary>The response, or null when Bitvavo does not list the market.</summary>
    private async Task<JsonDocument?> GetAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        using var response = await http.GetAsync($"https://api.bitvavo.com/v2/{path}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            if (body.Contains("market", StringComparison.OrdinalIgnoreCase) && (int)response.StatusCode is 400 or 404) return null; // unknown market
            throw new HttpRequestException($"Bitvavo {(int)response.StatusCode}");
        }
        return JsonDocument.Parse(body);
    }

    private static string Market(string symbol) =>
        $"{(MarketSymbols.CryptoPair(symbol) ?? throw new ArgumentException($"Not a crypto symbol: {symbol}")).Base}-EUR";

    private static long Ms(DateOnly d) => new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
    private static long Ms(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
