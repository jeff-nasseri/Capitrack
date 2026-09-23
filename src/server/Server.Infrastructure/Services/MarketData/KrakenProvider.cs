using System.Text.Json;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// Kraken public market data (crypto, USD/EUR pairs). No key. The OHLC endpoint returns at most the
/// 720 most recent candles — about two years of daily closes, 30 days of hourly ones.
/// </summary>
public sealed class KrakenProvider(HttpClient http, TimeProvider? clock = null, TimeSpan? rateLimit = null) : IPriceProvider
{
    private readonly RateGate _gate = new(rateLimit ?? TimeSpan.FromSeconds(1.1)); // public endpoints: ~1 request/second

    /// <summary>Kraken's own names for a few assets.</summary>
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase) { ["BTC"] = "XBT", ["DOGE"] = "XDG" };

    public ProviderInfo Info { get; } = new(
        "kraken", "Kraken", [AssetClass.Crypto], RequiresApiKey: false, ApiKeyEnvVar: null, ApiKeyUrl: null,
        Website: "https://docs.kraken.com/api/",
        Limits: "Free, no key. About 1 request/second. Only the 720 most recent candles: ~2 years of daily closes.");

    public bool IsConfigured => true;

    public bool Supports(string symbol, AssetClass assetClass) =>
        assetClass == AssetClass.Crypto && MarketSymbols.CryptoPair(symbol) is { } p && p.Quote is "USD" or "EUR" && !p.Base.Contains('-');

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (pair, quote) = Pair(symbol);
        var since = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var rows = await OhlcAsync(pair, 1440, since, ct);
        // a daily candle opens at 00:00 UTC: its close is that day's close
        var closes = rows.Select(r => new DailyClose(DateOnly.FromDateTime(r.Open), r.Close))
            .Where(c => c.Date >= from && c.Date <= to).OrderBy(c => c.Date).ToList();
        return closes.Count == 0 ? null : new PriceSeries(symbol, quote, Info.Id, closes);
    }

    public async Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto) || utc < (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddHours(-715)) return null;
        var (pair, quote) = Pair(symbol);
        var since = new DateTimeOffset(utc.AddHours(-2), TimeSpan.Zero).ToUnixTimeSeconds();
        var candle = (await OhlcAsync(pair, 60, since, ct)).FirstOrDefault(r => r.Open <= utc && utc < r.Open.AddHours(1));
        return candle is null ? null : new PricePoint(candle.Close, quote, Info.Id, "hour", candle.Open);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto)) return null;
        var (pair, quote) = Pair(symbol);
        using var doc = await GetAsync($"Ticker?pair={pair}", ct);
        var ticker = Result(doc);
        if (ticker is null) return null;
        var last = JsonNumbers.ToDecimal(ticker.Value.GetProperty("c")[0]) ?? 0;
        var open = JsonNumbers.ToDecimal(ticker.Value.GetProperty("o")) ?? 0;
        return new QuoteDto { Symbol = symbol, Price = last, Currency = quote, Name = symbol, ChangePercent = open > 0 ? (double)((last - open) / open * 100) : 0 };
    }

    private sealed record Candle(DateTime Open, decimal Close);

    private async Task<List<Candle>> OhlcAsync(string pair, int interval, long since, CancellationToken ct)
    {
        using var doc = await GetAsync($"OHLC?pair={pair}&interval={interval}&since={since}", ct);
        var rows = Result(doc);
        var list = new List<Candle>();
        if (rows is null) return list;
        foreach (var r in rows.Value.EnumerateArray())
            if (JsonNumbers.ToDecimal(r[4]) is { } close) // [time, open, high, low, close, vwap, volume, count]
                list.Add(new Candle(DateTimeOffset.FromUnixTimeSeconds(r[0].GetInt64()).UtcDateTime, close));
        return list;
    }

    /// <summary>The single result entry (Kraken keys it by its own pair name, e.g. XXBTZUSD); null for an unknown pair.</summary>
    private static JsonElement? Result(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("error", out var errors) && errors.GetArrayLength() > 0)
        {
            var message = errors[0].GetString() ?? "";
            if (message.Contains("Unknown asset pair", StringComparison.OrdinalIgnoreCase)) return null;
            throw new HttpRequestException($"Kraken: {message}");
        }
        foreach (var p in doc.RootElement.GetProperty("result").EnumerateObject())
            if (p.Name != "last") return p.Value;
        return null;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        using var response = await http.GetAsync($"https://api.kraken.com/0/public/{path}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Kraken {(int)response.StatusCode}");
        return JsonDocument.Parse(body);
    }

    private static (string Pair, string Quote) Pair(string symbol)
    {
        var p = MarketSymbols.CryptoPair(symbol) ?? throw new ArgumentException($"Not a crypto symbol: {symbol}");
        return ($"{Names.GetValueOrDefault(p.Base, p.Base)}{p.Quote}", p.Quote);
    }
}
