using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Api.Services;

/// <summary>
/// Quote retrieval with the same 5-minute price_cache + stale-fallback behaviour
/// as the original prices route.
/// </summary>
public class PriceService(CapitrackDbContext db, YahooFinanceClient yahoo)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>Fresh-cache → live fetch → stale-cache. Returns null only if nothing is available at all.</summary>
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

    /// <summary>Cache-only read (no live fetch) — used by the daily-wealth snapshot.</summary>
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
            db.PriceCache.Add(new PriceCache
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

    private static QuoteDto ToDto(PriceCache c, bool stale) => new()
    {
        Symbol = c.Symbol, Price = c.Price, Currency = c.Currency,
        Name = c.Name, ChangePercent = c.ChangePercent, Stale = stale ? true : null
    };
}
