using System.Collections.Concurrent;

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

    /// <summary>A month of hourly candles per symbol, fetched once: pricing many transactions costs one request per month.</summary>
    private readonly ConcurrentDictionary<string, (DateTime FetchedAt, List<HistoryPointDto> Points, string? Currency)> _hourly = new();

    public async Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        if (utc < now.AddDays(-729) || utc > now) return null;
        var month = new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var key = $"{symbol}|{month:yyyy-MM}";
        // the current month keeps growing: refetch it when the cached copy may lack the hour asked for
        if (!_hourly.TryGetValue(key, out var cached) || cached.FetchedAt < utc.AddHours(1))
        {
            await _gate.WaitAsync(ct);
            var end = month.AddMonths(1) < now ? month.AddMonths(1) : now;
            var (points, currency) = await yahoo.ChartRangeAsync(symbol, month, end, "1h");
            _hourly[key] = cached = (now, points, currency);
        }
        var candle = cached.Points.FirstOrDefault(p => p.Close is not null && p.Date <= utc && utc < p.Date.AddHours(1));
        return candle is null ? null : new PricePoint(candle.Close!.Value, cached.Currency ?? "USD", Info.Id, "hour", candle.Date);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        return await yahoo.QuoteAsync(symbol);
    }

    /// <summary>One v7 request for all symbols; the ones it leaves out are retried one by one (chart fallback).</summary>
    public async Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        var batch = await yahoo.QuotesAsync(symbols);
        var quotes = new Dictionary<string, QuoteDto>();
        foreach (var symbol in symbols)
        {
            if (batch.TryGetValue(symbol, out var quote)) { quotes[symbol] = quote; continue; }
            if (await QuoteAsync(symbol, ct) is { Price: > 0 } single) quotes[symbol] = single;
        }
        return quotes;
    }

    private static DateTime Start(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
