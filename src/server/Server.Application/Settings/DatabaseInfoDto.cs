namespace Server.Application.Settings;

/// <summary>The current database path and whether the file exists.</summary>
/// <param name="Path">The database file path.</param>
/// <param name="Exists">True when the database file exists.</param>
public record DatabaseInfoDto(string Path, bool Exists);
