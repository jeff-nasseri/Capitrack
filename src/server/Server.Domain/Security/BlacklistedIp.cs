namespace Server.Domain.Security;

/// <summary>
/// An IP address blocked from signing in — either manually by the user (no expiry) or
/// automatically after too many failed attempts (temporary, with an expiry).
/// </summary>
public sealed class BlacklistedIp : Entity<int>
{
    /// <summary>The blocked IP address.</summary>
    public string IpAddress { get; private set; } = "";

    /// <summary>The username associated with an automatic block (the account being brute-forced), if any.</summary>
    public string? Username { get; private set; }

    /// <summary>Why the IP was blocked.</summary>
    public string Reason { get; private set; } = "";

    /// <summary>When the block was created (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>When the block lifts (UTC); null means it never expires (a manual block).</summary>
    public DateTime? ExpiresAt { get; private set; }

    private BlacklistedIp() { }

    /// <summary>A manual, permanent block added by the user.</summary>
    public static BlacklistedIp Manual(string ipAddress, string? reason) =>
        new()
        {
            IpAddress = ipAddress.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? "Manually blocked" : reason.Trim(),
            ExpiresAt = null,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>An automatic, temporary block after repeated failed attempts.</summary>
    public static BlacklistedIp Temporary(string ipAddress, string? username, string reason, DateTime expiresAt) =>
        new()
        {
            IpAddress = ipAddress.Trim(),
            Username = string.IsNullOrWhiteSpace(username) ? null : username.Trim(),
            Reason = reason,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>True when this block is still in effect at the given instant.</summary>
    public bool IsActive(DateTime now) => ExpiresAt is null || ExpiresAt > now;
}
