namespace Server.Domain.Security;

/// <summary>An immutable audit record of a single sign-in attempt (successful or not).</summary>
public sealed class LoginAttempt : Entity<int>
{
    /// <summary>The username that was tried.</summary>
    public string Username { get; private set; } = "";

    /// <summary>The client IP the attempt came from (real IP via X-Forwarded-For).</summary>
    public string IpAddress { get; private set; } = "";

    /// <summary>Whether the attempt succeeded.</summary>
    public bool Success { get; private set; }

    /// <summary>A short machine-readable outcome, e.g. "success", "invalid_password", "invalid_2fa", "ip_blocked".</summary>
    public string Reason { get; private set; } = "";

    /// <summary>The requesting user agent, if any (truncated).</summary>
    public string? UserAgent { get; private set; }

    /// <summary>When the attempt happened (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    private LoginAttempt() { }

    /// <summary>Creates an audit record for a sign-in attempt.</summary>
    public static LoginAttempt Record(string? username, string? ipAddress, bool success, string reason, string? userAgent)
    {
        static string Cap(string s, int max) => s.Length > max ? s[..max] : s;
        return new LoginAttempt
        {
            Username = Cap((username ?? "").Trim(), 128),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : Cap(ipAddress.Trim(), 64),
            Success = success,
            Reason = reason ?? "",
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : Cap(userAgent.Trim(), 256),
            CreatedAt = DateTime.UtcNow
        };
    }
}
