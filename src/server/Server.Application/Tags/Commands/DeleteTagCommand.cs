using Server.Application.Common.Exceptions;

namespace Server.Application.Tags.Commands;

/// <summary>Deletes a tag by id.</summary>
/// <param name="Id">The tag's identifier.</param>
public record DeleteTagCommand(int Id) : IRequest;

/// <summary>Handles <see cref="DeleteTagCommand"/>.</summary>
public sealed class DeleteTagHandler(
    ITagRepository tags,
    IUnitOfWork uow,
    ILogger<DeleteTagHandler> logger)
    : IRequestHandler<DeleteTagCommand>
{
    /// <summary>Loads and deletes the tag, then persists the change.</summary>
    public async Task Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteTagCommand));

        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");
        tags.Remove(tag);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
