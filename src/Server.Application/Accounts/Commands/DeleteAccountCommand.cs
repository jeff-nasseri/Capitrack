using Server.Application.Common.Exceptions;

namespace Server.Application.Accounts.Commands;

public record DeleteAccountCommand(int Id) : IRequest;

public sealed class DeleteAccountHandler(IAccountRepository accounts, IUnitOfWork uow)
    : IRequestHandler<DeleteAccountCommand>
{
    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Account not found");
        accounts.Remove(account);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
