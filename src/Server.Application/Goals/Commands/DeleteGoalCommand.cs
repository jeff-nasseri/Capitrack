using Server.Application.Common.Exceptions;

namespace Server.Application.Goals.Commands;

public record DeleteGoalCommand(int Id) : IRequest;

public sealed class DeleteGoalHandler(IGoalRepository goals, IUnitOfWork uow)
    : IRequestHandler<DeleteGoalCommand>
{
    public async Task Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await goals.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Goal not found");
        goals.Remove(goal);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
