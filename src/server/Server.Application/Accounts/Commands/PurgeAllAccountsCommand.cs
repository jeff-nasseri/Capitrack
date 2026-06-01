namespace Server.Application.Accounts.Commands;

/// <summary>Removes all accounts and their dependent data.</summary>
public record PurgeAllAccountsCommand : IRequest;

/// <summary>Handles <see cref="PurgeAllAccountsCommand"/>.</summary>
public sealed class PurgeAllAccountsHandler(
    IAccountRepository accounts,
    IUnitOfWork uow,
    ILogger<PurgeAllAccountsHandler> logger)
    : IRequestHandler<PurgeAllAccountsCommand>
{
    /// <summary>Purges all accounts and persists the change.</summary>
    public async Task Handle(PurgeAllAccountsCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(PurgeAllAccountsCommand));

        await accounts.PurgeAllAsync(cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
