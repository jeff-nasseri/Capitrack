using System.Collections.Concurrent;
using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services;

/// <summary>
/// Quote retrieval (through the configured providers, with fallback) with a 5-minute price cache + stale
/// fallback. Quotes missing from the cache are fetched together (one request per provider), and a symbol
/// no provider could price is not asked for again for 10 minutes.
/// </summary>
public sealed class PriceService(CapitrackDbContext db, IMarketDataService market) : IPriceService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MissTtl = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, DateTime> Misses = new();

    public async Task<QuoteDto?> GetQuoteAsync(string symbol) =>
        (await GetQuotesAsync([symbol])).GetValueOrDefault(symbol.ToUpperInvariant());

    public async Task<Dictionary<string, QuoteDto>> GetQuotesAsync(IEnumerable<string> symbols)
    {
        var wanted = symbols.Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0).Distinct().ToList();
        var cached = await db.PriceCache.Where(p => wanted.Contains(p.Symbol)).ToDictionaryAsync(p => p.Symbol);
        var now = DateTime.UtcNow;
        var result = new Dictionary<string, QuoteDto>();
        var fetch = new List<string>();
        foreach (var s in wanted)
        {
            if (cached.TryGetValue(s, out var c) && c.UpdatedAt > now - CacheTtl) result[s] = ToDto(c, stale: false);
            else if (Misses.TryGetValue(s, out var missedAt) && missedAt > now - MissTtl) { if (c != null) result[s] = ToDto(c, stale: true); }
            else fetch.Add(s);
        }
        if (fetch.Count == 0) return result;

        var live = await market.QuotesAsync(fetch);
        foreach (var s in fetch)
        {
            if (live.TryGetValue(s, out var quote))
            {
                Misses.TryRemove(s, out _);
                quote.Symbol = s;
                Upsert(cached.GetValueOrDefault(s), quote, now);
                result[s] = quote;
            }
            else
            {
                Misses[s] = now;
                if (cached.TryGetValue(s, out var c)) result[s] = ToDto(c, stale: true);
            }
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); } // a concurrent request cached them first
        return result;
    }

    public async Task<QuoteDto?> GetCachedAsync(string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        var c = await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == symbol);
        return c != null ? ToDto(c, stale: false) : null;
    }

    public async Task UpsertAsync(QuoteDto q)
    {
        Upsert(await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == q.Symbol), q, DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private void Upsert(PriceCacheRecord? existing, QuoteDto q, DateTime now)
    {
        if (existing == null)
        {
            db.PriceCache.Add(new PriceCacheRecord
            {
                Symbol = q.Symbol, Price = q.Price, Currency = q.Currency,
                Name = q.Name, ChangePercent = q.ChangePercent, UpdatedAt = now
            });
            return;
        }
        existing.Price = q.Price;
        existing.Currency = q.Currency;
        existing.Name = q.Name;
        existing.ChangePercent = q.ChangePercent;
        existing.UpdatedAt = now;
    }

    private static QuoteDto ToDto(PriceCacheRecord c, bool stale) => new()
    {
        Symbol = c.Symbol, Price = c.Price, Currency = c.Currency,
        Name = c.Name, ChangePercent = c.ChangePercent, Stale = stale ? true : null
    };
}
