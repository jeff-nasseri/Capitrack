namespace Server.Application.Settings;

/// <summary>The result of a refresh request.</summary>
/// <param name="Message">A human-readable status message.</param>
/// <param name="DbPath">The active database file path.</param>
public record RefreshResultDto(string Message, string DbPath);
