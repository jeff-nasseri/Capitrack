using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Application.Transactions;
using Server.Domain.Transactions;

namespace Server.Application.Transactions.Commands;

public record CreateTransactionCommand(
    int? AccountId, string? Symbol, string? Type, double? Quantity, double? Price,
    double? Fee, string? Currency, string? Date, string? Notes, List<int>? TagIds)
    : IRequest<TransactionDto>;

public sealed class CreateTransactionValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionValidator()
    {
        RuleFor(x => x.AccountId).NotNull().GreaterThan(0).WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Symbol).NotEmpty().WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Type).Must(TransactionType.IsValid).WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Date).NotEmpty().WithMessage("account_id, symbol, type, and date are required");
    }
}

public sealed class CreateTransactionHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow)
    : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        if (!await accounts.ExistsAsync(request.AccountId!.Value, cancellationToken))
            throw new NotFoundException("Account not found");

        var tx = Transaction.Create(
            request.AccountId.Value,
            Symbol.Create(request.Symbol),
            TransactionType.From(request.Type),
            Quantity.Create(request.Quantity ?? 0),
            request.Price ?? 0,
            request.Fee ?? 0,
            CurrencyCode.CreateOrDefault(request.Currency, CurrencyCode.Eur),
            TradeDate.Create(request.Date),
            request.Notes);

        await transactions.AddAsync(tx, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        await transactions.ReplaceTagsAsync(tx.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var tagIds = await transactions.TagIdsAsync(tx.Id, cancellationToken);
        var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
            .OrderBy(tg => tg.Name)
            .Select(tg => tg.ToDto())
            .ToList();
        var account = await accounts.GetAsync(tx.AccountId, cancellationToken);

        return tx.ToDto(account?.Name, tagDtos);
    }
}
