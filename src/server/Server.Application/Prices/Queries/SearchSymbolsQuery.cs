namespace Server.Application.Prices.Queries;

/// <summary>Searches for symbols matching a query.</summary>
/// <param name="Query">The free-text search query.</param>
public record SearchSymbolsQuery(string Query) : IRequest<List<SearchResultDto>>;

/// <summary>Handles <see cref="SearchSymbolsQuery"/>.</summary>
public sealed class SearchSymbolsQueryHandler(
    IYahooFinanceClient yahoo,
    ILogger<SearchSymbolsQueryHandler> logger)
    : IRequestHandler<SearchSymbolsQuery, List<SearchResultDto>>
{
    /// <summary>Delegates the symbol search to the Yahoo Finance client.</summary>
    public async Task<List<SearchResultDto>> Handle(SearchSymbolsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(SearchSymbolsQuery));
        return await yahoo.SearchAsync(request.Query);
    }
}
