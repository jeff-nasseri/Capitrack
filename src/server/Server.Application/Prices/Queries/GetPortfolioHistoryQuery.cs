namespace Server.Application.Prices.Queries;

/// <summary>Returns the portfolio value history for an optional account over a period.</summary>
/// <param name="AccountId">An optional account filter.</param>
/// <param name="Period">The period key (e.g. 1w, 1m, 1y, max).</param>
public record GetPortfolioHistoryQuery(int? AccountId, string? Period) : IRequest<List<PortfolioHistoryPointDto>>;

/// <summary>Handles <see cref="GetPortfolioHistoryQuery"/>.</summary>
public sealed class GetPortfolioHistoryQueryHandler(
    IWealthService wealth,
    ILogger<GetPortfolioHistoryQueryHandler> logger)
    : IRequestHandler<GetPortfolioHistoryQuery, List<PortfolioHistoryPointDto>>
{
    /// <summary>Delegates to the wealth service to compute the portfolio history.</summary>
    public async Task<List<PortfolioHistoryPointDto>> Handle(GetPortfolioHistoryQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetPortfolioHistoryQuery));
        return await wealth.PortfolioHistoryAsync(request.AccountId, request.Period);
    }
}
