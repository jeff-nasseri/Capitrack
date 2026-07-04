using Server.Application.Common.Exceptions;

namespace Server.Application.Security.Commands;

/// <summary>Removes an IP blacklist entry (manual or automatic) by id.</summary>
/// <param name="Id">The entry identifier.</param>
public record RemoveBlacklistCommand(int Id) : IRequest;

/// <summary>Handles <see cref="RemoveBlacklistCommand"/>.</summary>
public sealed class RemoveBlacklistHandler(
    IBlacklistRepository blacklist,
    IUnitOfWork uow,
    ILogger<RemoveBlacklistHandler> logger)
    : IRequestHandler<RemoveBlacklistCommand>
{
    /// <summary>Deletes the blacklist entry, unblocking the IP.</summary>
    public async Task Handle(RemoveBlacklistCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(RemoveBlacklistCommand));

        var entry = await blacklist.GetAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException("Blacklist entry not found");
        blacklist.Remove(entry);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
