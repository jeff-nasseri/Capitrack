using Server.Domain.Security;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence for the IP blacklist (manual + automatic blocks).</summary>
public interface IBlacklistRepository
{
    /// <summary>Returns the active (non-expired) block for an IP, or null.</summary>
    Task<BlacklistedIp?> GetActiveByIpAsync(string ipAddress, DateTime now, CancellationToken ct = default);

    /// <summary>Returns an existing MANUAL (never-expiring) block for an IP, or null (used to avoid duplicate manual entries).</summary>
    Task<BlacklistedIp?> GetManualByIpAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>Returns all active (non-expired) blocks, newest first.</summary>
    Task<IReadOnlyList<BlacklistedIp>> ListActiveAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Returns a block by id, or null.</summary>
    Task<BlacklistedIp?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Tracks a new block for insertion.</summary>
    Task AddAsync(BlacklistedIp entry, CancellationToken ct = default);

    /// <summary>Marks a block for deletion.</summary>
    void Remove(BlacklistedIp entry);
}
