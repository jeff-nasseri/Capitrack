using Server.Application.Common.Exceptions;

namespace Server.Application.Tags.Commands;

public record DeleteTagCommand(int Id) : IRequest;

public sealed class DeleteTagHandler(ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<DeleteTagCommand>
{
    public async Task Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");
        tags.Remove(tag);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
