using Server.Application.Tags;
using Server.Domain.Accounts;

namespace Server.Application.Accounts.Commands;

/// <summary>Creates a new account with optional tag links.</summary>
/// <param name="Name">The account name (required).</param>
/// <param name="Type">The account type string.</param>
/// <param name="Currency">The base currency code.</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Icon">The icon identifier.</param>
/// <param name="Color">The hex color.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record CreateAccountCommand(
    string? Name, string? Type, string? Currency, string? Description,
    string? Icon, string? Color, List<int>? TagIds) : IRequest<AccountDto>;

/// <summary>Validates <see cref="CreateAccountCommand"/>.</summary>
public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateAccountValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Account name is required.");
}

/// <summary>Handles <see cref="CreateAccountCommand"/>.</summary>
public sealed class CreateAccountHandler(
    IAccountRepository accounts,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<CreateAccountHandler> logger)
    : IRequestHandler<CreateAccountCommand, AccountDto>
{
    /// <summary>Creates the account, persists it, links its tags and returns the resulting DTO.</summary>
    public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(CreateAccountCommand));

        var account = Account.Create(
            request.Name,
            AccountType.From(request.Type),
            CurrencyCode.CreateOrDefault(request.Currency, CurrencyCode.Eur),
            request.Description,
            request.Icon,
            Color.CreateOrDefault(request.Color));

        await accounts.AddAsync(account, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        await accounts.ReplaceTagsAsync(account.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var tagIds = await accounts.TagIdsAsync(account.Id, cancellationToken);
        var tagEntities = await tags.ByIdsAsync(tagIds, cancellationToken);
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<AccountDto>(account);
        return dto with { Tags = tagDtos };
    }
}
