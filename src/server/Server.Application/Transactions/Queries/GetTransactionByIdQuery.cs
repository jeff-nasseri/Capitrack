using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Application.Transactions;

namespace Server.Application.Transactions.Queries;

public record GetTransactionByIdQuery(int Id) : IRequest<TransactionDto>;

public sealed class GetTransactionByIdQueryHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts,
    ITagRepository tags)
    : IRequestHandler<GetTransactionByIdQuery, TransactionDto>
{
    public async Task<TransactionDto> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var t = await transactions.GetAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException("Transaction not found");

        var account = await accounts.GetAsync(t.AccountId, cancellationToken);
        var tagIds = await transactions.TagIdsAsync(t.Id, cancellationToken);
        var tagDtos = (await tags.ByIdsAsync(tagIds, cancellationToken))
            .OrderBy(tg => tg.Name)
            .Select(tg => tg.ToDto())
            .ToList();

        return t.ToDto(account?.Name, tagDtos);
    }
}
