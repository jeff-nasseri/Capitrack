namespace Server.Application.Common.Interfaces;

/// <summary>Low-level Yahoo Finance access.</summary>
public interface IYahooFinanceClient
{
    /// <summary>Fetches a live quote for a symbol, or null when unavailable.</summary>
    Task<QuoteDto?> QuoteAsync(string symbol);

    /// <summary>Fetches a price history series for a symbol from <paramref name="period1"/> at the given interval.</summary>
    Task<List<HistoryPointDto>> ChartAsync(string symbol, DateTime period1, string interval);

    /// <summary>Searches for symbols matching a query.</summary>
    Task<List<SearchResultDto>> SearchAsync(string query);
}
