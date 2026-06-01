namespace Server.Application.Prices.Queries;

/// <summary>Returns the dashboard summary (total wealth, gains and per-account breakdown).</summary>
public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

/// <summary>Handles <see cref="GetDashboardSummaryQuery"/>.</summary>
public sealed class GetDashboardSummaryQueryHandler(
    IWealthService wealth,
    ILogger<GetDashboardSummaryQueryHandler> logger)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    /// <summary>Delegates to the wealth service to compute the dashboard summary.</summary>
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetDashboardSummaryQuery));
        return await wealth.DashboardSummaryAsync();
    }
}
