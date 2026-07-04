namespace Server.Application.Security;

/// <summary>API representation of an audited sign-in attempt.</summary>
/// <param name="Id">The record identifier.</param>
/// <param name="Username">The username that was tried.</param>
/// <param name="IpAddress">The client IP the attempt came from.</param>
/// <param name="Success">Whether it succeeded.</param>
/// <param name="Reason">A short outcome code (e.g. "success", "invalid_password", "invalid_2fa", "ip_blocked").</param>
/// <param name="CreatedAt">When it happened (UTC).</param>
/// <param name="UserAgent">The requesting user agent, if captured.</param>
public record LoginAttemptDto(
    int Id, string Username, string IpAddress, bool Success, string Reason, DateTime CreatedAt, string? UserAgent);
