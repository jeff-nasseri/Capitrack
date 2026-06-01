using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Accounts.Queries;

/// <summary>Returns a single account with its attached tags.</summary>
/// <param name="Id">The account's identifier.</param>
public record GetAccountByIdQuery(int Id) : IRequest<AccountDto>;

/// <summary>Handles <see cref="GetAccountByIdQuery"/>.</summary>
public sealed class GetAccountByIdQueryHandler(
    IAccountRepository accounts,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetAccountByIdQueryHandler> logger)
    : IRequestHandler<GetAccountByIdQuery, AccountDto>
{
    /// <summary>Loads the account, resolves its tags and returns the resulting DTO.</summary>
    public async Task<AccountDto> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetAccountByIdQuery));

        var account = await accounts.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Account not found");

        var tagIds = await accounts.TagIdsAsync(account.Id, cancellationToken);
        var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<AccountDto>(account);
        return dto with { Tags = tagDtos };
    }
}
