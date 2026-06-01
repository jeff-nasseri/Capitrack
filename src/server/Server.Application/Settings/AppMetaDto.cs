namespace Server.Application.Settings;

/// <summary>Application metadata surfaced by the settings info endpoint.</summary>
/// <param name="DbPath">The active database file path.</param>
/// <param name="Version">The application version.</param>
/// <param name="AppName">The application name.</param>
/// <param name="Repository">The source repository URL.</param>
/// <param name="License">The license identifier.</param>
public record AppMetaDto(string DbPath, string Version, string AppName, string Repository, string License);
