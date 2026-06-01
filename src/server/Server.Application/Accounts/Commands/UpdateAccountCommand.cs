using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Domain.Accounts;

namespace Server.Application.Accounts.Commands;

/// <summary>Updates an existing account and its tag links.</summary>
/// <param name="Id">The account's identifier.</param>
/// <param name="Name">The new name, or null to keep the current value.</param>
/// <param name="Type">The new type string, or null to keep the current value.</param>
/// <param name="Currency">The new currency code, or null to keep the current value.</param>
/// <param name="Description">The new description, or null to keep the current value.</param>
/// <param name="Icon">The new icon identifier, or null to keep the current value.</param>
/// <param name="Color">The new hex color, or null to keep the current value.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record UpdateAccountCommand(
    int Id, string? Name, string? Type, string? Currency, string? Description,
    string? Icon, string? Color, List<int>? TagIds) : IRequest<AccountDto>;

/// <summary>Handles <see cref="UpdateAccountCommand"/>.</summary>
public sealed class UpdateAccountHandler(
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpdateAccountHandler> logger)
    : IRequestHandler<UpdateAccountCommand, AccountDto>
{
    /// <summary>Loads, updates and persists the account, re-links its tags and returns the resulting DTO.</summary>
    public async Task<AccountDto> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpdateAccountCommand));

        var account = await accounts.GetAsync(request.Id, cancellationToken)
                      ?? throw new NotFoundException("Account not found");

        account.Update(
            request.Name ?? account.Name,
            request.Type != null ? AccountType.From(request.Type) : account.Type,
            request.Currency != null ? CurrencyCode.Create(request.Currency) : account.Currency,
            request.Description ?? account.Description,
            request.Icon ?? account.Icon,
            request.Color != null ? Color.Create(request.Color) : account.Color);

        await uow.SaveChangesAsync(cancellationToken);

        await accounts.ReplaceTagsAsync(request.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var tagIds = await accounts.TagIdsAsync(request.Id, cancellationToken);
        var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<AccountDto>(account);
        return dto with { Tags = tagDtos };
    }
}
