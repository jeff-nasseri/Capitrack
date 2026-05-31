namespace Server.Application.Goals.Commands;

public record DeleteAllGoalsCommand : IRequest;

public sealed class DeleteAllGoalsHandler(IGoalRepository goals, IUnitOfWork uow)
    : IRequestHandler<DeleteAllGoalsCommand>
{
    public async Task Handle(DeleteAllGoalsCommand request, CancellationToken cancellationToken)
    {
        await goals.RemoveAllAsync(cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
