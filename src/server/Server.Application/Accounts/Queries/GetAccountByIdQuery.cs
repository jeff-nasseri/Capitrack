using Server.Application.Accounts;
using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Accounts.Queries;

public record GetAccountByIdQuery(int Id) : IRequest<AccountDto>;

public sealed class GetAccountByIdQueryHandler(IAccountRepository accounts, ITagRepository tags)
    : IRequestHandler<GetAccountByIdQuery, AccountDto>
{
    public async Task<AccountDto> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Account not found");

        var tagIds = await accounts.TagIdsAsync(account.Id, cancellationToken);
        var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return account.ToDto(tagDtos);
    }
}
