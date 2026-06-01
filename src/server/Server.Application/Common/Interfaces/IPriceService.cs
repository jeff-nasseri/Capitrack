namespace Server.Application.Common.Interfaces;

/// <summary>Quote retrieval with a 5-minute cache and stale fallback.</summary>
public interface IPriceService
{
    /// <summary>Returns a quote (live, cached, or stale) for a symbol, or null.</summary>
    Task<QuoteDto?> GetQuoteAsync(string symbol);

    /// <summary>Returns the cached quote for a symbol, or null.</summary>
    Task<QuoteDto?> GetCachedAsync(string symbol);

    /// <summary>Inserts or updates the cached quote.</summary>
    Task UpsertAsync(QuoteDto quote);
}
