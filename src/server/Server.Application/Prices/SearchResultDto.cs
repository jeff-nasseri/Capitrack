namespace Server.Application.Prices;

/// <summary>A symbol search result.</summary>
/// <param name="Symbol">The matched symbol.</param>
/// <param name="Name">The instrument's display name.</param>
/// <param name="Type">The instrument type, if known.</param>
/// <param name="Exchange">The listing exchange, if known.</param>
public record SearchResultDto(string Symbol, string Name, string? Type, string? Exchange);
