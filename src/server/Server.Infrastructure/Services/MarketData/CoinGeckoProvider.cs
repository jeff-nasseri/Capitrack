using System.Text.Json;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// CoinGecko (crypto). Works without a key (public API) or with a free Demo key
/// (<c>COINGECKO_DEMO_API_KEY</c>) for a higher rate limit. Free access only covers the past 365
/// days of history; ranges up to 90 days come back hourly (≤ 1 day: 5-minutely), longer ones daily.
/// Only coins in a fixed id map are priced: a ticker alone is ambiguous (many tokens share one), and
/// a wrong match would silently value a holding at another coin's price.
/// </summary>
public sealed class CoinGeckoProvider : IPriceProvider
{
    private static readonly Dictionary<string, string> Ids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin", ["ETH"] = "ethereum", ["SOL"] = "solana", ["ADA"] = "cardano", ["XRP"] = "ripple",
        ["DOGE"] = "dogecoin", ["TRX"] = "tron", ["BNB"] = "binancecoin", ["USDC"] = "usd-coin", ["USDT"] = "tether",
        ["DAI"] = "dai", ["LTC"] = "litecoin", ["BCH"] = "bitcoin-cash", ["ETC"] = "ethereum-classic", ["DOT"] = "polkadot",
        ["AVAX"] = "avalanche-2", ["LINK"] = "chainlink", ["XLM"] = "stellar", ["ATOM"] = "cosmos", ["POL"] = "polygon-ecosystem-token",
        ["MATIC"] = "matic-network", ["XMR"] = "monero", ["DASH"] = "dash", ["ZEC"] = "zcash", ["ALGO"] = "algorand",
        ["NEAR"] = "near", ["UNI"] = "uniswap", ["AAVE"] = "aave", ["TON"] = "the-open-network", ["SHIB"] = "shiba-inu",
        ["ARB"] = "arbitrum", ["OP"] = "optimism", ["SUI"] = "sui", ["APT"] = "aptos", ["HBAR"] = "hedera-hashgraph"
    };

    private const int HistoryDays = 365;
    private readonly HttpClient _http;
    private readonly string? _key;
    private readonly RateGate _gate;
    private readonly TimeProvider _clock;

    public CoinGeckoProvider(HttpClient http, string? demoApiKey, TimeProvider? clock = null, TimeSpan? rateLimit = null)
    {
        _http = http;
        _clock = clock ?? TimeProvider.System;
        _key = string.IsNullOrWhiteSpace(demoApiKey) ? null : demoApiKey.Trim();
        _gate = new RateGate(rateLimit ?? (_key is null ? TimeSpan.FromSeconds(6.5) : TimeSpan.FromSeconds(2.1))); // ~9/min public, ~28/min demo
    }

    public ProviderInfo Info { get; } = new(
        "coingecko", "CoinGecko", [AssetClass.Crypto], RequiresApiKey: false,
        ApiKeyEnvVar: "COINGECKO_DEMO_API_KEY", ApiKeyUrl: "https://www.coingecko.com/en/developers/dashboard",
        Website: "https://www.coingecko.com/en/api",
        Limits: "Free, no key needed (a free Demo key raises the rate limit to ~30 calls/min, 10,000/month). History limited to the past 365 days; hourly data for ranges up to 90 days.");

    public bool IsConfigured => true;

    public bool Supports(string symbol, AssetClass assetClass) =>
        assetClass == AssetClass.Crypto && MarketSymbols.CryptoPair(symbol) is { } p && Ids.ContainsKey(p.Base) && p.Quote is "USD" or "EUR";

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (id, vs) = Coin(symbol);
        var earliest = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime.AddDays(-HistoryDays + 1));
        if (to < earliest) return null;
        if (from < earliest) from = earliest;

        var points = await RangeAsync(id, vs, Start(from), Start(to.AddDays(1)), ct);
        if (points.Count == 0) return null;
        // a point at 00:00 UTC is the previous day's close; otherwise the last point of a UTC day is its close
        var closes = points
            .GroupBy(p => DateOnly.FromDateTime(p.At.AddSeconds(-1)))
            .Where(g => g.Key >= from && g.Key <= to)
            .Select(g => new DailyClose(g.Key, g.MaxBy(p => p.At).Price))
            .OrderBy(c => c.Date).ToList();
        return closes.Count == 0 ? null : new PriceSeries(symbol, vs.ToUpperInvariant(), Info.Id, closes);
    }

    public async Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto) || utc < _clock.GetUtcNow().UtcDateTime.AddDays(-HistoryDays + 1)) return null;
        var (id, vs) = Coin(symbol);
        var points = await RangeAsync(id, vs, utc.AddHours(-1), utc.AddHours(1), ct); // ≤ 1 day → 5-minute points
        var best = points.Where(p => p.At <= utc).MaxBy(p => p.At) ?? points.MinBy(p => p.At);
        return best is null ? null : new PricePoint(best.Price, vs.ToUpperInvariant(), Info.Id, "hour", best.At);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (!Supports(symbol, AssetClass.Crypto)) return null;
        var (id, vs) = Coin(symbol);
        using var doc = await GetAsync($"simple/price?ids={id}&vs_currencies={vs}&include_24hr_change=true", ct);
        if (!doc.RootElement.TryGetProperty(id, out var coin) || !coin.TryGetProperty(vs, out var price)) return null;
        var change = coin.TryGetProperty($"{vs}_24h_change", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0;
        return new QuoteDto { Symbol = symbol, Price = JsonNumbers.ToDecimal(price) ?? 0, Currency = vs.ToUpperInvariant(), Name = symbol, ChangePercent = change };
    }

    private (string Id, string Vs) Coin(string symbol)
    {
        var pair = MarketSymbols.CryptoPair(symbol) ?? throw new ArgumentException($"Not a crypto symbol: {symbol}");
        return (Ids[pair.Base], pair.Quote.ToLowerInvariant());
    }

    private sealed record Point(DateTime At, decimal Price);

    private async Task<List<Point>> RangeAsync(string id, string vs, DateTime from, DateTime to, CancellationToken ct)
    {
        using var doc = await GetAsync($"coins/{id}/market_chart/range?vs_currency={vs}&from={Unix(from)}&to={Unix(to)}", ct);
        var list = new List<Point>();
        if (!doc.RootElement.TryGetProperty("prices", out var prices)) return list;
        foreach (var p in prices.EnumerateArray())
            if (JsonNumbers.ToDecimal(p[1]) is { } price)
                list.Add(new Point(DateTimeOffset.FromUnixTimeMilliseconds(p[0].GetInt64()).UtcDateTime, price));
        return list;
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.coingecko.com/api/v3/{path}");
        if (_key is not null) request.Headers.Add("x-cg-demo-api-key", _key);
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"CoinGecko {(int)response.StatusCode}: {ErrorMessage(body)}");
        return JsonDocument.Parse(body);
    }

    /// <summary>CoinGecko's explanation ({"error":{"status":{"error_message":…}}} or {"error":"…"}), else the start of the body.</summary>
    private static string ErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String) return Trim(error.GetString() ?? "");
                if (error.TryGetProperty("status", out var status) && status.TryGetProperty("error_message", out var message))
                    return Trim(message.GetString() ?? "");
            }
        }
        catch (JsonException) { /* not JSON: fall through */ }
        return Trim(body);
    }

    private static DateTime Start(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static long Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
    private static string Trim(string s) => s.Length > 200 ? s[..200] : s;
}
