namespace Server.Application.Prices.Queries;

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler(IWealthService wealth)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
        => await wealth.DashboardSummaryAsync();
}
