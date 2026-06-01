using Server.Application.Accounts;
using Server.Application.Tags;
using Server.Domain.Accounts;

namespace Server.Application.Accounts.Commands;

public record CreateAccountCommand(
    string? Name, string? Type, string? Currency, string? Description,
    string? Icon, string? Color, List<int>? TagIds) : IRequest<AccountDto>;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Account name is required.");
}

public sealed class CreateAccountHandler(IAccountRepository accounts, ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<CreateAccountCommand, AccountDto>
{
    public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
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
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return account.ToDto(tagDtos);
    }
}
