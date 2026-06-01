namespace Server.Application.Settings;

/// <summary>The result of changing the database path.</summary>
/// <param name="Message">A human-readable status message.</param>
/// <param name="Path">The new database file path.</param>
/// <param name="Exists">True when the database file exists at the new path.</param>
public record DatabaseUpdateDto(string Message, string Path, bool Exists);
