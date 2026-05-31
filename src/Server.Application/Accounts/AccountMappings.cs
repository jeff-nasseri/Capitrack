using Server.Domain.Accounts;

namespace Server.Application.Accounts;

public static class AccountMappings
{
    public static AccountDto ToDto(this Account a, List<TagDto> tags) => new(
        a.Id, a.Name, a.Type.Value, a.Currency.Value, a.Description,
        a.Icon, a.Color.Value, a.CreatedAt, a.UpdatedAt, tags);
}
