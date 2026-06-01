namespace Server.Application.Prices.Commands;

public record SaveDailyWealthCommand : IRequest<DailyWealthSnapshotDto>;

public sealed class SaveDailyWealthHandler(IWealthService wealth)
    : IRequestHandler<SaveDailyWealthCommand, DailyWealthSnapshotDto>
{
    public async Task<DailyWealthSnapshotDto> Handle(SaveDailyWealthCommand request, CancellationToken cancellationToken)
        => await wealth.SaveDailyWealthAsync();
}
