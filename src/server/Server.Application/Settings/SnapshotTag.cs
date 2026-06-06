namespace Server.Application.Settings;

/// <summary>A single tag inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Id">The tag's original identifier (remapped on import).</param>
/// <param name="Name">The tag's name.</param>
/// <param name="Color">The tag's hex color.</param>
public record SnapshotTag(
    int Id,
    string Name,
    string Color);
