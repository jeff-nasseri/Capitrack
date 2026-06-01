namespace Server.Application.Tags;

/// <summary>API representation of a tag.</summary>
/// <param name="Id">The tag's identifier.</param>
/// <param name="Name">The tag's name.</param>
/// <param name="Color">The tag's hex color.</param>
/// <param name="CreatedAt">When the tag was created.</param>
public record TagDto(int Id, string Name, string Color, DateTime CreatedAt);
