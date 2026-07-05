namespace Server.Application.Auth;

/// <summary>The authenticated session, returned by login and session queries.</summary>
/// <param name="Username">The signed-in user's name.</param>
/// <param name="BaseCurrency">The user's base currency code.</param>
/// <param name="TwoFactorEnabled">Whether two-factor authentication is active for the user.</param>
/// <param name="SessionLifetimeMinutes">How long the session stays valid without activity, in minutes.</param>
public record SessionDto(string Username, string BaseCurrency, bool TwoFactorEnabled, int SessionLifetimeMinutes);
