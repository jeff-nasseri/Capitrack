namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// Yahoo Finance (all asset classes) through the existing unofficial client. No key, broad
/// coverage and long history, but no official API or terms — kept as a fallback, not the default.
/// </summary>
public sealed class YahooProvider(IYahooFinanceClient yahoo, TimeProvider? clock = null) : IPriceProvider
{
    private readonly RateGate _gate = new(TimeSpan.FromMilliseconds(400));

    public ProviderInfo Info { get; } = new(
        "yahoo-finance", "Yahoo Finance", [AssetClass.Crypto, AssetClass.Stock, AssetClass.Metal, AssetClass.Fx],
        RequiresApiKey: false, ApiKeyEnvVar: null, ApiKeyUrl: null, Website: "https://finance.yahoo.com",
        Limits: "No key. Unofficial endpoints with no published terms or rate limits; may break without notice. Hourly history for the last 730 days.");

    public bool IsConfigured => true;

    public bool Supports(string symbol, AssetClass assetClass) => true;

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        var (points, currency) = await yahoo.ChartRangeAsync(symbol, Start(from), Start(to.AddDays(1)), "1d");
        // a daily candle's timestamp is the start of its trading day (00:00 UTC for crypto)
        var closes = points.Where(p => p.Close is not null)
            .GroupBy(p => DateOnly.FromDateTime(p.Date))
            .Where(g => g.Key >= from && g.Key <= to)
            .Select(g => new DailyClose(g.Key, g.Last().Close!.Value))
            .OrderBy(c => c.Date).ToList();
        return closes.Count == 0 ? null : new PriceSeries(symbol, currency ?? "USD", Info.Id, closes);
    }

    public async Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        if (utc < (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime.AddDays(-729)) return null;
        await _gate.WaitAsync(ct);
        var (points, currency) = await yahoo.ChartRangeAsync(symbol, utc.AddHours(-2), utc.AddHours(2), "1h");
        var candle = points.Where(p => p.Close is not null && p.Date <= utc && utc < p.Date.AddHours(1)).FirstOrDefault();
        return candle is null ? null : new PricePoint(candle.Close!.Value, currency ?? "USD", Info.Id, "hour", candle.Date);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        return await yahoo.QuoteAsync(symbol);
    }

    private static DateTime Start(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
