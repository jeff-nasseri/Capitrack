namespace Server.Application.Settings;

/// <summary>A single account inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Id">The account's original identifier (remapped on import).</param>
/// <param name="Name">The account's name.</param>
/// <param name="Type">The account type string.</param>
/// <param name="Currency">The account's base currency code.</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Icon">The icon identifier.</param>
/// <param name="Color">The account's hex color.</param>
/// <param name="TagIds">The original tag ids linked to the account (remapped on import).</param>
public record SnapshotAccount(
    int Id,
    string Name,
    string Type,
    string Currency,
    string? Description,
    string? Icon,
    string Color,
    List<int> TagIds);
