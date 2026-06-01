namespace Server.Application.Prices.Queries;

public record GetPortfolioHistoryQuery(int? AccountId, string? Period) : IRequest<List<PortfolioHistoryPointDto>>;

public sealed class GetPortfolioHistoryQueryHandler(IWealthService wealth)
    : IRequestHandler<GetPortfolioHistoryQuery, List<PortfolioHistoryPointDto>>
{
    public async Task<List<PortfolioHistoryPointDto>> Handle(GetPortfolioHistoryQuery request, CancellationToken cancellationToken)
        => await wealth.PortfolioHistoryAsync(request.AccountId, request.Period);
}
