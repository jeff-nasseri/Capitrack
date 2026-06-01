using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services;

/// <summary>Quote retrieval with a 5-minute price-cache + stale fallback.</summary>
public sealed class PriceService(CapitrackDbContext db, IYahooFinanceClient yahoo) : IPriceService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<QuoteDto?> GetQuoteAsync(string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        var cutoff = DateTime.UtcNow - CacheTtl;
        var fresh = await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == symbol && p.UpdatedAt > cutoff);
        if (fresh != null) return ToDto(fresh, stale: false);

        var live = await yahoo.QuoteAsync(symbol);
        if (live != null)
        {
            await UpsertAsync(live);
            return live;
        }

        var stale = await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == symbol);
        return stale != null ? ToDto(stale, stale: true) : null;
    }

    public async Task<QuoteDto?> GetCachedAsync(string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        var c = await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == symbol);
        return c != null ? ToDto(c, stale: false) : null;
    }

    public async Task UpsertAsync(QuoteDto q)
    {
        var existing = await db.PriceCache.FirstOrDefaultAsync(p => p.Symbol == q.Symbol);
        if (existing == null)
        {
            db.PriceCache.Add(new PriceCacheRecord
            {
                Symbol = q.Symbol, Price = q.Price, Currency = q.Currency,
                Name = q.Name, ChangePercent = q.ChangePercent, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Price = q.Price;
            existing.Currency = q.Currency;
            existing.Name = q.Name;
            existing.ChangePercent = q.ChangePercent;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static QuoteDto ToDto(PriceCacheRecord c, bool stale) => new()
    {
        Symbol = c.Symbol, Price = c.Price, Currency = c.Currency,
        Name = c.Name, ChangePercent = c.ChangePercent, Stale = stale ? true : null
    };
}
