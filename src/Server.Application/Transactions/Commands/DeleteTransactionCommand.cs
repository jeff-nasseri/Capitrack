using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

public record DeleteTransactionCommand(int Id) : IRequest;

public sealed class DeleteTransactionHandler(ITransactionRepository transactions, IUnitOfWork uow)
    : IRequestHandler<DeleteTransactionCommand>
{
    public async Task Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var t = await transactions.GetAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Transaction not found");
        transactions.Remove(t);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
