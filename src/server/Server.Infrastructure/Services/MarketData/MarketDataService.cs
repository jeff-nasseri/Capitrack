using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// Routes price requests to the providers chosen in settings — per asset class, in order — and
/// falls back to the next one when a provider fails, is not configured, cannot price the symbol or
/// lacks part of the range. Past daily closes are cached for good (they never change); a coverage
/// record per (symbol, provider) remembers what was already asked, so a provider is never asked
/// twice for days it has no close for. Providers only ever receive a symbol and dates.
/// </summary>
public sealed class MarketDataService(CapitrackDbContext db, IEnumerable<IPriceProvider> providers, ILogger<MarketDataService> log,
    TimeProvider? clock = null) : IMarketDataService
{
    private const string OrderKey = "market_data.provider_order";

    /// <summary>
    /// Official/keyless sources first; Yahoo (unofficial) as the last fallback where others exist. For crypto,
    /// Kraken (USD, ~1 request/s) leads, Bitvavo (full history, EUR) covers what is older than Kraken's two
    /// years, and CoinGecko (widest coverage, but ~9 calls/min and one year of history) comes after them.
    /// </summary>
    public static readonly IReadOnlyDictionary<AssetClass, string[]> DefaultOrder = new Dictionary<AssetClass, string[]>
    {
        [AssetClass.Crypto] = ["kraken", "bitvavo", "coingecko", "yahoo-finance"],
        [AssetClass.Stock] = ["yahoo-finance", "twelvedata"],
        [AssetClass.Metal] = ["yahoo-finance", "twelvedata"],
        [AssetClass.Fx] = ["ecb", "yahoo-finance"],
    };

    private readonly List<IPriceProvider> _providers = providers.ToList();

    /// <summary>One cache fill at a time per (symbol, provider) across requests and the background top-up.</summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FillLocks = new();

    // ---------------------------------------------------------------- daily closes

    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, string? providerId = null, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var lastFinal = Today().AddDays(-1); // today's close is not final: never cached
        var finalTo = to < lastFinal ? to : lastFinal;
        PriceSeries? best = null;

        foreach (var provider in await CandidatesAsync(symbol, providerId, ct))
        {
            try
            {
                if (from <= finalTo) await EnsureCachedAsync(provider, symbol, from, finalTo, ct);
                var closes = await CachedAsync(symbol, provider.Info.Id, from, finalTo, ct);
                if (closes.Count == 0) continue;
                var series = new PriceSeries(symbol, closes[0].Currency, provider.Info.Id,
                    closes.Select(c => new DailyClose(ParseDate(c.Date), c.Close)).ToList());
                if (Complete(series, from, finalTo)) return series;
                if (best is null || series.Closes.Count > best.Closes.Count) best = series; // partial: try the next provider
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                log.LogWarning("Price provider {Provider} failed for {Symbol} {From}..{To}: {Error}", provider.Info.Id, symbol, from, to, e.Message);
            }
        }
        return best;
    }

    /// <summary>Complete when it reaches within a week of both ends (markets close on weekends and holidays).</summary>
    private static bool Complete(PriceSeries s, DateOnly from, DateOnly to) =>
        s.Closes.Count > 0 && s.Closes[0].Date <= from.AddDays(7) && s.Closes[^1].Date >= to.AddDays(-7);

    private async Task EnsureCachedAsync(IPriceProvider provider, string symbol, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var gate = FillLocks.GetOrAdd($"{symbol}|{provider.Info.Id}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try { await FillAsync(provider, symbol, from, to, ct); }
        finally { gate.Release(); }
    }

    private async Task FillAsync(IPriceProvider provider, string symbol, DateOnly from, DateOnly to, CancellationToken ct)
    {
        // another request may have extended the coverage since this context last read it
        if (db.ChangeTracker.Entries<PriceCoverageRecord>().FirstOrDefault(e => e.Entity.Symbol == symbol && e.Entity.Provider == provider.Info.Id) is { } seen)
            await seen.ReloadAsync(ct);
        var coverage = await db.PriceCoverage.FindAsync([symbol, provider.Info.Id], ct);
        // only the days outside the covered interval are fetched; the gaps adjoin it, so it stays one interval
        var gaps = new List<(DateOnly From, DateOnly To)>();
        if (coverage is null) gaps.Add((from, to));
        else
        {
            var (coveredFrom, coveredTo) = (ParseDate(coverage.From), ParseDate(coverage.To));
            if (from < coveredFrom) gaps.Add((from, coveredFrom.AddDays(-1)));
            if (to > coveredTo) gaps.Add((coveredTo.AddDays(1), to));
        }
        if (gaps.Count == 0) return;

        foreach (var (gapFrom, gapTo) in gaps)
        {
            var series = await provider.DailyAsync(symbol, gapFrom, gapTo, ct); // throws on transport errors: nothing recorded, retried later
            if (series is null) continue;
            var existing = await db.PriceHistory
                .Where(p => p.Symbol == symbol && p.Provider == provider.Info.Id
                            && string.Compare(p.Date, Iso(gapFrom)) >= 0 && string.Compare(p.Date, Iso(gapTo)) <= 0)
                .ToDictionaryAsync(p => p.Date, ct);
            foreach (var close in series.Closes.Where(c => c.Date >= gapFrom && c.Date <= gapTo))
            {
                if (existing.TryGetValue(Iso(close.Date), out var row)) { row.Close = close.Close; row.Currency = series.Currency; }
                else db.PriceHistory.Add(new PriceHistoryRecord { Symbol = symbol, Provider = provider.Info.Id, Date = Iso(close.Date), Close = close.Close, Currency = series.Currency });
            }
        }

        var fetchFrom = coverage is null || from < ParseDate(coverage.From) ? from : ParseDate(coverage.From);
        var fetchTo = coverage is null || to > ParseDate(coverage.To) ? to : ParseDate(coverage.To);
        if (coverage is null)
            db.PriceCoverage.Add(new PriceCoverageRecord { Symbol = symbol, Provider = provider.Info.Id, From = Iso(fetchFrom), To = Iso(fetchTo), FetchedAt = DateTime.UtcNow });
        else
        {
            coverage.From = Iso(fetchFrom);
            coverage.To = Iso(fetchTo);
            coverage.FetchedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    private Task<List<PriceHistoryRecord>> CachedAsync(string symbol, string provider, DateOnly from, DateOnly to, CancellationToken ct) =>
        db.PriceHistory.AsNoTracking()
            .Where(p => p.Symbol == symbol && p.Provider == provider && string.Compare(p.Date, Iso(from)) >= 0 && string.Compare(p.Date, Iso(to)) <= 0)
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

    // ---------------------------------------------------------------- price at a moment

    public async Task<PricePoint?> PriceAtAsync(string symbol, DateTime utc, string? providerId = null, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        foreach (var provider in await CandidatesAsync(symbol, providerId, ct))
        {
            try
            {
                if (await provider.HourlyAtAsync(symbol, utc, ct) is { } point) return point;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Price provider {Provider} failed for {Symbol} at {At}: {Error}", provider.Info.Id, symbol, utc, e.Message);
            }
        }
        // documented fallback: the close of that UTC day
        var day = DateOnly.FromDateTime(utc);
        var series = await DailyAsync(symbol, day, day, providerId, ct);
        return series?.Closes.FirstOrDefault(c => c.Date == day) is { } close
            ? new PricePoint(close.Close, series.Currency, series.Provider, "day", day.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc))
            : null;
    }

    // ---------------------------------------------------------------- quotes & FX

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        foreach (var provider in await CandidatesAsync(symbol, null, ct))
        {
            try
            {
                if (await provider.QuoteAsync(symbol, ct) is { Price: > 0 } quote)
                {
                    await InPairCurrencyAsync(symbol, quote, ct);
                    return quote;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Price provider {Provider} failed to quote {Symbol}: {Error}", provider.Info.Id, symbol, e.Message);
            }
        }
        return null;
    }

    public async Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var order = await OrderAsync(ct); // read once: the groups below run in parallel and must not share the DbContext
        var groups = symbols.Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0).Distinct().GroupBy(MarketSymbols.Classify);
        var found = await Task.WhenAll(groups.Select(g => QuoteGroupAsync(g.Key, g.ToList(), order[g.Key], ct)));
        var quotes = found.SelectMany(d => d).ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var (symbol, quote) in quotes) await InPairCurrencyAsync(symbol, quote, ct);
        return quotes;
    }

    /// <summary>
    /// A crypto pair is quoted in its own quote currency (BTC-USD in USD) even when the provider that answered
    /// prices it in another (Bitvavo: EUR), converted at the latest ECB rate, so it compares with its cost.
    /// </summary>
    private async Task InPairCurrencyAsync(string symbol, QuoteDto quote, CancellationToken ct)
    {
        if (MarketSymbols.CryptoPair(symbol) is not { } pair || pair.Quote.Equals(quote.Currency, StringComparison.OrdinalIgnoreCase)) return;
        if (await FxRateAsync(quote.Currency, pair.Quote, Today(), ct) is not { } rate) return;
        quote.Price *= rate;
        quote.Currency = pair.Quote;
    }

    /// <summary>Walks one asset class's providers in order, asking each for every symbol still missing that it supports.</summary>
    private async Task<Dictionary<string, QuoteDto>> QuoteGroupAsync(AssetClass assetClass, List<string> symbols, List<string> order, CancellationToken ct)
    {
        var quotes = new Dictionary<string, QuoteDto>();
        foreach (var provider in order.Select(id => _providers.FirstOrDefault(p => p.Info.Id == id)).OfType<IPriceProvider>().Where(p => p.IsConfigured))
        {
            var ask = symbols.Where(s => !quotes.ContainsKey(s) && provider.Supports(s, assetClass)).ToList();
            if (ask.Count == 0) continue;
            try
            {
                foreach (var (symbol, quote) in await provider.QuotesAsync(ask, ct))
                    if (quote.Price > 0 && ask.Contains(symbol)) quotes[symbol] = quote;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Price provider {Provider} failed to quote {Count} symbols: {Error}", provider.Info.Id, ask.Count, e.Message);
            }
            if (quotes.Count == symbols.Count) break;
        }
        return quotes;
    }

    public async Task<decimal?> FxRateAsync(string from, string to, DateOnly date, CancellationToken ct = default)
    {
        from = from.Trim().ToUpperInvariant();
        to = to.Trim().ToUpperInvariant();
        if (from == to) return 1m;
        // the last published rate on or before the date (no rates on weekends and TARGET holidays)
        var series = await DailyAsync($"{from}{to}=X", date.AddDays(-10), date, null, ct);
        return series?.Closes.LastOrDefault(c => c.Date <= date)?.Close;
    }

    // ---------------------------------------------------------------- settings

    public async Task<List<MarketDataProviderDto>> ProvidersAsync(CancellationToken ct = default)
    {
        var order = await OrderAsync(ct);
        return _providers.Select(p =>
        {
            var positions = order
                .Where(kv => kv.Value.Contains(p.Info.Id))
                .ToDictionary(kv => kv.Key.ToString().ToLowerInvariant(), kv => kv.Value.IndexOf(p.Info.Id) + 1);
            return new MarketDataProviderDto(
                p.Info.Id, p.Info.Name, p.Info.Limits,
                Enabled: positions.Count > 0,
                RequiresApiKey: p.Info.RequiresApiKey,
                ApiKeySet: p.Info.ApiKeyEnvVar is not null && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(p.Info.ApiKeyEnvVar)),
                Website: p.Info.Website,
                IsDefault: positions.ContainsValue(1),
                AssetClasses: p.Info.Classes.Select(c => c.ToString().ToLowerInvariant()).ToList(),
                Limits: p.Info.Limits,
                ApiKeyEnvVar: p.Info.ApiKeyEnvVar,
                ApiKeyUrl: p.Info.ApiKeyUrl,
                Positions: positions,
                Configured: p.IsConfigured);
        }).ToList();
    }

    public async Task SetProviderOrderAsync(Dictionary<AssetClass, List<string>> order, CancellationToken ct = default)
    {
        foreach (var (assetClass, ids) in order)
            foreach (var id in ids)
                if (_providers.FirstOrDefault(p => p.Info.Id == id) is not { } p || !p.Info.Classes.Contains(assetClass))
                    throw new ArgumentException($"Provider \"{id}\" cannot price {assetClass.ToString().ToLowerInvariant()}.");

        var json = JsonSerializer.Serialize(order.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.Distinct().ToList()));
        var row = await db.AppSettings.FindAsync([OrderKey], ct);
        if (row is null) db.AppSettings.Add(new AppSettingRecord { Key = OrderKey, Value = json });
        else row.Value = json;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<AssetClass, List<string>>> OrderAsync(CancellationToken ct)
    {
        var order = DefaultOrder.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == OrderKey, ct);
        if (row is null) return order;
        try
        {
            foreach (var (key, ids) in JsonSerializer.Deserialize<Dictionary<string, List<string>>>(row.Value) ?? [])
                if (Enum.TryParse<AssetClass>(key, true, out var assetClass)) order[assetClass] = ids;
        }
        catch (JsonException) { /* a corrupt setting falls back to the defaults */ }
        return order;
    }

    private async Task<List<IPriceProvider>> CandidatesAsync(string symbol, string? providerId, CancellationToken ct)
    {
        var assetClass = MarketSymbols.Classify(symbol);
        if (providerId is not null)
            return _providers.Where(p => p.Info.Id == providerId && p.IsConfigured && p.Supports(symbol, assetClass)).ToList();
        var order = await OrderAsync(ct);
        return order[assetClass]
            .Select(id => _providers.FirstOrDefault(p => p.Info.Id == id))
            .Where(p => p is not null && p.IsConfigured && p.Supports(symbol, assetClass))
            .Select(p => p!).ToList();
    }

    private DateOnly Today() => DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseDate(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
