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
        return ticker is null ? null : ToQuote(symbol, quote, ticker.Value);
    }

    /// <summary>
    /// One Ticker request for every listed pair. Kraken rejects the whole request if one pair is unknown and
    /// answers under its own pair names (XBTUSD → XXBTZUSD), so pairs are checked and mapped via AssetPairs.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        var quotes = new Dictionary<string, QuoteDto>();
        var wanted = symbols.Where(s => Supports(s, AssetClass.Crypto)).Select(s => (Symbol: s, Pair: Pair(s))).ToList();
        if (wanted.Count == 0) return quotes;
        var pairs = await PairsAsync(ct);
        var listed = wanted.Where(w => pairs.ContainsKey(w.Pair.Pair)).ToList();
        if (listed.Count == 0) return quotes;

        using var doc = await GetAsync($"Ticker?pair={string.Join(',', listed.Select(w => w.Pair.Pair).Distinct())}", ct);
        if (doc.RootElement.TryGetProperty("error", out var errors) && errors.GetArrayLength() > 0)
            throw new HttpRequestException($"Kraken: {errors[0].GetString()}");
        var result = doc.RootElement.GetProperty("result");
        foreach (var w in listed)
            if (result.TryGetProperty(pairs[w.Pair.Pair], out var ticker) && ToQuote(w.Symbol, w.Pair.Quote, ticker) is { } quote)
                quotes[w.Symbol] = quote;
        return quotes;
    }

    private static QuoteDto? ToQuote(string symbol, string currency, JsonElement ticker)
    {
        var last = JsonNumbers.ToDecimal(ticker.GetProperty("c")[0]) ?? 0;
        if (last <= 0) return null;
        var open = JsonNumbers.ToDecimal(ticker.GetProperty("o")) ?? 0; // today's opening price (00:00 UTC)
        return new QuoteDto { Symbol = symbol, Price = last, Currency = currency, Name = symbol, ChangePercent = open > 0 ? (double)((last - open) / open * 100) : 0 };
    }

    private readonly SemaphoreSlim _pairsLock = new(1, 1);
    private (DateTime At, Dictionary<string, string> Keys)? _pairs;

    /// <summary>Kraken's tradable pairs: altname (XBTUSD) → the name its answers use (XXBTZUSD). Refreshed daily.</summary>
    private async Task<Dictionary<string, string>> PairsAsync(CancellationToken ct)
    {
        await _pairsLock.WaitAsync(ct);
        try
        {
            var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            if (_pairs is { } cached && cached.At > now.AddDays(-1)) return cached.Keys;
            using var doc = await GetAsync("AssetPairs", ct);
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in doc.RootElement.GetProperty("result").EnumerateObject())
                if (pair.Value.TryGetProperty("altname", out var alt) && alt.GetString() is { } name) keys[name] = pair.Name;
            _pairs = (now, keys);
            return keys;
        }
        finally { _pairsLock.Release(); }
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
