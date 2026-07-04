using Server.Domain.Security;

namespace Server.Infrastructure.Services;

/// <summary>
/// Brute-force protection shared by both login steps: 3 genuine failures from an IP within a
/// 15-minute window trigger a 15-minute IP block. Every attempt is audited.
/// </summary>
public sealed class LoginSecurityService(
    ILoginAttemptRepository attempts,
    IBlacklistRepository blacklist,
    IUnitOfWork uow) : ILoginSecurityService
{
    private const int MaxFailures = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<bool> IsIpBlockedAsync(string ipAddress, CancellationToken ct = default) =>
        await blacklist.GetActiveByIpAsync(ipAddress, DateTime.UtcNow, ct) is not null;

    /// <inheritdoc />
    public async Task RecordAttemptAsync(string? username, string ipAddress, bool success, string reason, string? userAgent, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await attempts.AddAsync(LoginAttempt.Record(username, ipAddress, success, reason, userAgent), ct);
        await uow.SaveChangesAsync(ct);

        // Only genuine credential/2FA failures escalate to a block — never the "ip_blocked" rejections
        // themselves (otherwise a block could perpetually renew itself).
        if (success || reason == "ip_blocked") return;

        var failures = await attempts.CountRecentFailuresAsync(ipAddress, now - Window, ct);
        if (failures < MaxFailures) return;

        // Don't stack blocks — only add one if the IP isn't already actively blocked.
        if (await blacklist.GetActiveByIpAsync(ipAddress, now, ct) is not null) return;

        await blacklist.AddAsync(
            BlacklistedIp.Temporary(ipAddress, username,
                $"Auto-blocked after {failures} failed sign-in attempts", now + BlockDuration),
            ct);
        await uow.SaveChangesAsync(ct);
    }
}
