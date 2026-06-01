using Server.Application.Common.Exceptions;

namespace Server.Application.Goals.Commands;

/// <summary>Deletes a goal by id.</summary>
/// <param name="Id">The goal's identifier.</param>
public record DeleteGoalCommand(int Id) : IRequest;

/// <summary>Handles <see cref="DeleteGoalCommand"/>.</summary>
public sealed class DeleteGoalHandler(
    IGoalRepository goals,
    IUnitOfWork uow,
    ILogger<DeleteGoalHandler> logger)
    : IRequestHandler<DeleteGoalCommand>
{
    /// <summary>Loads and deletes the goal, then persists the change.</summary>
    public async Task Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteGoalCommand));

        var goal = await goals.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Goal not found");
        goals.Remove(goal);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
