namespace Server.Application.Goals.Commands;

/// <summary>Removes all goals.</summary>
public record DeleteAllGoalsCommand : IRequest;

/// <summary>Handles <see cref="DeleteAllGoalsCommand"/>.</summary>
public sealed class DeleteAllGoalsHandler(
    IGoalRepository goals,
    IUnitOfWork uow,
    ILogger<DeleteAllGoalsHandler> logger)
    : IRequestHandler<DeleteAllGoalsCommand>
{
    /// <summary>Removes all goals and persists the change.</summary>
    public async Task Handle(DeleteAllGoalsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteAllGoalsCommand));

        await goals.RemoveAllAsync(cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
