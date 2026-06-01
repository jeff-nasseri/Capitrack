using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Transactions.Queries;

/// <summary>Returns a single transaction with its account name and tags.</summary>
/// <param name="Id">The transaction's identifier.</param>
public record GetTransactionByIdQuery(int Id) : IRequest<TransactionDto>;

/// <summary>Handles <see cref="GetTransactionByIdQuery"/>.</summary>
public sealed class GetTransactionByIdQueryHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetTransactionByIdQueryHandler> logger)
    : IRequestHandler<GetTransactionByIdQuery, TransactionDto>
{
    /// <summary>Loads the transaction, resolves its account name and tags, and returns the resulting DTO.</summary>
    public async Task<TransactionDto> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetTransactionByIdQuery));

        var t = await transactions.GetAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Transaction not found");

        var account = await accounts.GetAsync(t.AccountId, cancellationToken);
        var tagIds = await transactions.TagIdsAsync(t.Id, cancellationToken);
        var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
            .OrderBy(tg => tg.Name)
            .Select(tg => mapper.Map<TagDto>(tg))
            .ToList();

        var dto = mapper.Map<TransactionDto>(t);
        return dto with { AccountName = account?.Name, Tags = tagDtos };
    }
}
