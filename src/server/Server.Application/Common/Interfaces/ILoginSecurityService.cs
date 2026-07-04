namespace Server.Application.Common.Interfaces;

/// <summary>
/// Centralises the brute-force protection shared by the password and 2FA login steps:
/// checking whether an IP is blocked, auditing every attempt, and auto-blocking an IP
/// after too many failures. Keeps the two login handlers free of duplicated security logic.
/// </summary>
public interface ILoginSecurityService
{
    /// <summary>True when the IP currently has an active (non-expired) blacklist entry.</summary>
    Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Records a sign-in attempt. On a genuine credential/2FA failure it evaluates the
    /// 3-strikes-in-15-minutes rule and adds a 15-minute IP block when the threshold is hit.
    /// </summary>
    Task RecordAttemptAsync(string? username, string ipAddress, bool success, string reason, string? userAgent, CancellationToken ct = default);
}
