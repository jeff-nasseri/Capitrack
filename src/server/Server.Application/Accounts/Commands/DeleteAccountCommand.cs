using Server.Application.Common.Exceptions;

namespace Server.Application.Accounts.Commands;

/// <summary>Deletes an account by id.</summary>
/// <param name="Id">The account's identifier.</param>
public record DeleteAccountCommand(int Id) : IRequest;

/// <summary>Handles <see cref="DeleteAccountCommand"/>.</summary>
public sealed class DeleteAccountHandler(
    IAccountRepository accounts,
    IUnitOfWork uow,
    ILogger<DeleteAccountHandler> logger)
    : IRequestHandler<DeleteAccountCommand>
{
    /// <summary>Loads and deletes the account, then persists the change.</summary>
    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteAccountCommand));

        var account = await accounts.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Account not found");
        accounts.Remove(account);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
