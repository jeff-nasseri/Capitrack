using Server.Application.Common.Exceptions;

namespace Server.Application.Transactions.Commands;

/// <summary>Deletes a transaction by id.</summary>
/// <param name="Id">The transaction's identifier.</param>
public record DeleteTransactionCommand(int Id) : IRequest;

/// <summary>Handles <see cref="DeleteTransactionCommand"/>.</summary>
public sealed class DeleteTransactionHandler(
    ITransactionRepository transactions,
    IUnitOfWork uow,
    ILogger<DeleteTransactionHandler> logger)
    : IRequestHandler<DeleteTransactionCommand>
{
    /// <summary>Loads and deletes the transaction, then persists the change.</summary>
    public async Task Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(DeleteTransactionCommand));

        var t = await transactions.GetAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Transaction not found");
        transactions.Remove(t);
        await uow.SaveChangesAsync(cancellationToken);
    }
}
