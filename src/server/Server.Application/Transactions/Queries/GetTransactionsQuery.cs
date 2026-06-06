using Server.Application.Tags;

namespace Server.Application.Transactions.Queries;

/// <summary>Returns transactions matching the optional account/symbol/search/type filters and paging.</summary>
/// <param name="AccountId">An optional account filter.</param>
/// <param name="Symbol">An optional exact-symbol filter.</param>
/// <param name="Search">An optional case-insensitive CONTAINS filter on the symbol.</param>
/// <param name="Type">An optional exact transaction-type filter (e.g. "buy").</param>
/// <param name="Limit">An optional page size.</param>
/// <param name="Offset">An optional page offset.</param>
public record GetTransactionsQuery(int? AccountId, string? Symbol, string? Search, string? Type, int? Limit, int? Offset)
    : IRequest<List<TransactionDto>>;

/// <summary>Handles <see cref="GetTransactionsQuery"/>.</summary>
public sealed class GetTransactionsQueryHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetTransactionsQueryHandler> logger)
    : IRequestHandler<GetTransactionsQuery, List<TransactionDto>>
{
    /// <summary>Loads the matching transactions, resolves account names and tags, and returns the resulting DTOs.</summary>
    public async Task<List<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetTransactionsQuery));

        var items = await transactions.ListAsync(request.AccountId, request.Symbol, request.Search, request.Type, request.Limit, request.Offset, cancellationToken);
        var names = (await accounts.ListAsync(cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var result = new List<TransactionDto>(items.Count);
        foreach (var t in items)
        {
            var tagIds = await transactions.TagIdsAsync(t.Id, cancellationToken);
            var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
                .OrderBy(tg => tg.Name)
                .Select(tg => mapper.Map<TagDto>(tg))
                .ToList();
            var dto = mapper.Map<TransactionDto>(t);
            result.Add(dto with { AccountName = names.GetValueOrDefault(t.AccountId), Tags = tagDtos });
        }
        return result;
    }
}
