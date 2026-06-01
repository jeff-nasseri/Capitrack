using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Transactions.Commands;

/// <summary>Updates an existing transaction and its tag links.</summary>
/// <param name="Id">The transaction's identifier.</param>
/// <param name="Symbol">The new symbol, or null to keep the current value.</param>
/// <param name="Type">The new type string, or null to keep the current value.</param>
/// <param name="Quantity">The new quantity, or null to keep the current value.</param>
/// <param name="Price">The new unit price, or null to keep the current value.</param>
/// <param name="Fee">The new fee, or null to keep the current value.</param>
/// <param name="Currency">The new currency code, or null to keep the current value.</param>
/// <param name="Date">The new trade date, or null to keep the current value.</param>
/// <param name="Notes">The new notes, or null to keep the current value.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record UpdateTransactionCommand(
    int Id, string? Symbol, string? Type, double? Quantity, double? Price,
    double? Fee, string? Currency, string? Date, string? Notes, List<int>? TagIds)
    : IRequest<TransactionDto>;

/// <summary>Handles <see cref="UpdateTransactionCommand"/>.</summary>
public sealed class UpdateTransactionHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpdateTransactionHandler> logger)
    : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    /// <summary>Loads, updates and persists the transaction, re-links its tags and returns the resulting DTO.</summary>
    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpdateTransactionCommand));

        var t = await transactions.GetAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Transaction not found");

        var symbol = request.Symbol ?? t.Symbol.Value;
        var type = request.Type ?? t.Type.Value;
        var quantity = request.Quantity ?? t.Quantity.Value;
        var price = request.Price ?? t.Price;
        var fee = request.Fee ?? t.Fee;
        var currency = request.Currency ?? t.Currency.Value;
        var date = request.Date ?? t.Date.Value;
        var notes = request.Notes ?? t.Notes;

        t.Update(
            Symbol.Create(symbol),
            TransactionType.From(type),
            Quantity.Create(quantity),
            price,
            fee,
            CurrencyCode.Create(currency),
            TradeDate.Create(date),
            notes);

        await uow.SaveChangesAsync(cancellationToken);

        await transactions.ReplaceTagsAsync(request.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var tagIds = await transactions.TagIdsAsync(request.Id, cancellationToken);
        var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
            .OrderBy(tg => tg.Name)
            .Select(tg => mapper.Map<TagDto>(tg))
            .ToList();
        var account = await accounts.GetAsync(t.AccountId, cancellationToken);

        var dto = mapper.Map<TransactionDto>(t);
        return dto with { AccountName = account?.Name, Tags = tagDtos };
    }
}
