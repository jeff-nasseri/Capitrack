using Server.Application.Tags;
using Server.Application.Transactions;

namespace Server.Application.Transactions.Queries;

public record GetTransactionsQuery(int? AccountId, string? Symbol, int? Limit, int? Offset)
    : IRequest<List<TransactionDto>>;

public sealed class GetTransactionsQueryHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags)
    : IRequestHandler<GetTransactionsQuery, List<TransactionDto>>
{
    public async Task<List<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var items = await transactions.ListAsync(request.AccountId, request.Symbol, request.Limit, request.Offset, cancellationToken);
        var names = (await accounts.ListAsync(cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var result = new List<TransactionDto>(items.Count);
        foreach (var t in items)
        {
            var tagIds = await transactions.TagIdsAsync(t.Id, cancellationToken);
            var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
                .OrderBy(tg => tg.Name)
                .Select(tg => tg.ToDto())
                .ToList();
            result.Add(t.ToDto(names.GetValueOrDefault(t.AccountId), tagDtos));
        }
        return result;
    }
}
