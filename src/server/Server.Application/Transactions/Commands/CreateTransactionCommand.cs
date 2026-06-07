using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Domain.Transactions;

namespace Server.Application.Transactions.Commands;

/// <summary>Creates a new transaction on an account with optional tag links.</summary>
/// <param name="AccountId">The owning account's identifier (required).</param>
/// <param name="Symbol">The traded symbol (required).</param>
/// <param name="Type">The transaction type string (required).</param>
/// <param name="Quantity">The traded quantity.</param>
/// <param name="Price">The unit price.</param>
/// <param name="Fee">The transaction fee.</param>
/// <param name="Currency">The transaction currency code.</param>
/// <param name="Date">The trade date (required, yyyy-MM-dd).</param>
/// <param name="Notes">Free-text notes.</param>
/// <param name="IsStaked">Whether this transaction represents staked crypto.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record CreateTransactionCommand(
    int? AccountId, string? Symbol, string? Type, double? Quantity, double? Price,
    double? Fee, string? Currency, string? Date, string? Notes, bool? IsStaked, List<int>? TagIds)
    : IRequest<TransactionDto>;

/// <summary>Validates <see cref="CreateTransactionCommand"/>.</summary>
public sealed class CreateTransactionValidator : AbstractValidator<CreateTransactionCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateTransactionValidator()
    {
        RuleFor(x => x.AccountId).NotNull().GreaterThan(0).WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Symbol).NotEmpty().WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Type).Must(TransactionType.IsValid).WithMessage("account_id, symbol, type, and date are required");
        RuleFor(x => x.Date).NotEmpty().WithMessage("account_id, symbol, type, and date are required");
    }
}

/// <summary>Handles <see cref="CreateTransactionCommand"/>.</summary>
public sealed class CreateTransactionHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<CreateTransactionHandler> logger)
    : IRequestHandler<CreateTransactionCommand, TransactionDto>
{
    /// <summary>Creates the transaction, persists it, links its tags and returns the resulting DTO.</summary>
    public async Task<TransactionDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(CreateTransactionCommand));

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
            request.Notes,
            request.IsStaked ?? false);

        await transactions.AddAsync(tx, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        await transactions.ReplaceTagsAsync(tx.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var tagIds = await transactions.TagIdsAsync(tx.Id, cancellationToken);
        var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
            .OrderBy(tg => tg.Name)
            .Select(tg => mapper.Map<TagDto>(tg))
            .ToList();
        var account = await accounts.GetAsync(tx.AccountId, cancellationToken);

        var dto = mapper.Map<TransactionDto>(tx);
        return dto with { AccountName = account?.Name, Tags = tagDtos };
    }
}
