using Server.Application.Accounts;
using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Domain.Accounts;

namespace Server.Application.Accounts.Commands;

public record UpdateAccountCommand(
    int Id, string? Name, string? Type, string? Currency, string? Description,
    string? Icon, string? Color, List<int>? TagIds) : IRequest<AccountDto>;

public sealed class UpdateAccountHandler(IAccountRepository accounts, ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<UpdateAccountCommand, AccountDto>
{
    public async Task<AccountDto> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
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
        var tagDtos = tagEntities.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return account.ToDto(tagDtos);
    }
}
