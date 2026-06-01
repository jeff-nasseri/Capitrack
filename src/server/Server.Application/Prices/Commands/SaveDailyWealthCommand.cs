namespace Server.Application.Prices.Commands;

/// <summary>Computes and stores today's wealth snapshot.</summary>
public record SaveDailyWealthCommand : IRequest<DailyWealthSnapshotDto>;

/// <summary>Handles <see cref="SaveDailyWealthCommand"/>.</summary>
public sealed class SaveDailyWealthHandler(
    IWealthService wealth,
    ILogger<SaveDailyWealthHandler> logger)
    : IRequestHandler<SaveDailyWealthCommand, DailyWealthSnapshotDto>
{
    /// <summary>Delegates to the wealth service to compute and persist the snapshot.</summary>
    public async Task<DailyWealthSnapshotDto> Handle(SaveDailyWealthCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(SaveDailyWealthCommand));
        return await wealth.SaveDailyWealthAsync();
    }
}
