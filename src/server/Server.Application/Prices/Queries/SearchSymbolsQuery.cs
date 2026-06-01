namespace Server.Application.Prices.Queries;

public record SearchSymbolsQuery(string Query) : IRequest<List<SearchResultDto>>;

public sealed class SearchSymbolsQueryHandler(IYahooFinanceClient yahoo)
    : IRequestHandler<SearchSymbolsQuery, List<SearchResultDto>>
{
    public async Task<List<SearchResultDto>> Handle(SearchSymbolsQuery request, CancellationToken cancellationToken)
        => await yahoo.SearchAsync(request.Query);
}
