using Server.Application.Tags;

namespace Server.Application.Accounts;

/// <summary>API representation of an account, including its attached tags.</summary>
/// <param name="Id">The account's identifier.</param>
/// <param name="Name">The account's name.</param>
/// <param name="Type">The account type string.</param>
/// <param name="Currency">The account's base currency code.</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Icon">The icon identifier.</param>
/// <param name="Color">The account's hex color.</param>
/// <param name="CreatedAt">When the account was created.</param>
/// <param name="UpdatedAt">When the account was last updated.</param>
/// <param name="Tags">The tags attached to the account.</param>
public record AccountDto(
    int Id, string Name, string Type, string Currency, string Description,
    string Icon, string Color, DateTime CreatedAt, DateTime UpdatedAt, List<TagDto> Tags);
