using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Application.Transactions;

namespace Server.Application.Transactions.Commands;

public record UpdateTransactionCommand(
    int Id, string? Symbol, string? Type, double? Quantity, double? Price,
    double? Fee, string? Currency, string? Date, string? Notes, List<int>? TagIds)
    : IRequest<TransactionDto>;

public sealed class UpdateTransactionHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow)
    : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
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
            .Select(tg => tg.ToDto())
            .ToList();
        var account = await accounts.GetAsync(t.AccountId, cancellationToken);

        return t.ToDto(account?.Name, tagDtos);
    }
}
