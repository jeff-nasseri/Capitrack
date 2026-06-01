namespace Server.Application.Settings;

/// <summary>About information for the application.</summary>
/// <param name="Name">The application name.</param>
/// <param name="Description">A short description.</param>
/// <param name="Version">The application version.</param>
/// <param name="License">The license identifier.</param>
/// <param name="Repository">The source repository URL.</param>
/// <param name="Author">The author's name.</param>
/// <param name="OpenSource">Whether the application is open source.</param>
public record AboutDto(string Name, string Description, string Version, string License, string Repository, string Author, bool OpenSource);
