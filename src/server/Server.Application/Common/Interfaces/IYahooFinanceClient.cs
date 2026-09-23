namespace Server.Application.Common.Interfaces;

/// <summary>Low-level Yahoo Finance access.</summary>
public interface IYahooFinanceClient
{
    /// <summary>Fetches a live quote for a symbol, or null when unavailable.</summary>
    Task<QuoteDto?> QuoteAsync(string symbol);

    /// <summary>Live quotes for several symbols in one v7 request (upper-case symbol → quote); unknown symbols are left out.</summary>
    Task<Dictionary<string, QuoteDto>> QuotesAsync(IReadOnlyList<string> symbols);

    /// <summary>Fetches a price history series for a symbol from <paramref name="period1"/> at the given interval.</summary>
    Task<List<HistoryPointDto>> ChartAsync(string symbol, DateTime period1, string interval);

    /// <summary>Price points for [<paramref name="from"/>, <paramref name="to"/>) at an interval ("1d", "1h"), with the series' currency.</summary>
    Task<(List<HistoryPointDto> Points, string? Currency)> ChartRangeAsync(string symbol, DateTime from, DateTime to, string interval);

    /// <summary>Searches for symbols matching a query.</summary>
    Task<List<SearchResultDto>> SearchAsync(string query);
}
