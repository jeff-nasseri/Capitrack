using Server.Application.Tags;

namespace Server.Application.Accounts.Queries;

/// <summary>Returns all accounts with their attached tags.</summary>
public record GetAccountsQuery : IRequest<List<AccountDto>>;

/// <summary>Handles <see cref="GetAccountsQuery"/>.</summary>
public sealed class GetAccountsQueryHandler(
    IAccountRepository accounts,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetAccountsQueryHandler> logger)
    : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    /// <summary>Loads all accounts, resolves their tags and returns the resulting DTOs.</summary>
    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetAccountsQuery));

        var all = await accounts.ListAsync(cancellationToken);
        var result = new List<AccountDto>(all.Count);
        foreach (var account in all)
        {
            var tagIds = await accounts.TagIdsAsync(account.Id, cancellationToken);
            var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
            var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();
            var dto = mapper.Map<AccountDto>(account);
            result.Add(dto with { Tags = tagDtos });
        }
        return result;
    }
}
