namespace Server.Application.Accounts.Commands;

public record PurgeAllAccountsCommand : IRequest;

public sealed class PurgeAllAccountsHandler(IAccountRepository accounts, IUnitOfWork uow)
    : IRequestHandler<PurgeAllAccountsCommand>
{
    public async Task Handle(PurgeAllAccountsCommand request, CancellationToken cancellationToken)
    {
        await accounts.PurgeAllAsync(cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
