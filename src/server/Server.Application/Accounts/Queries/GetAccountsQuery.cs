using Server.Application.Accounts;
using Server.Application.Tags;

namespace Server.Application.Accounts.Queries;

public record GetAccountsQuery : IRequest<List<AccountDto>>;

public sealed class GetAccountsQueryHandler(IAccountRepository accounts, ITagRepository tags)
    : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var all = await accounts.ListAsync(cancellationToken);
        var result = new List<AccountDto>(all.Count);
        foreach (var account in all)
        {
            var tagIds = await accounts.TagIdsAsync(account.Id, cancellationToken);
            var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
            var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
            result.Add(account.ToDto(tagDtos));
        }
        return result;
    }
}
