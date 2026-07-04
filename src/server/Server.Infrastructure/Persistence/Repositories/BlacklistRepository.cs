using Server.Domain.Security;

namespace Server.Infrastructure.Persistence.Repositories;

/// <summary>EF Core persistence for the IP blacklist.</summary>
public sealed class BlacklistRepository(CapitrackDbContext db) : IBlacklistRepository
{
    /// <inheritdoc />
    public Task<BlacklistedIp?> GetActiveByIpAsync(string ipAddress, DateTime now, CancellationToken ct = default) =>
        db.BlacklistedIps
            .Where(b => b.IpAddress == ipAddress && (b.ExpiresAt == null || b.ExpiresAt > now))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<BlacklistedIp?> GetManualByIpAsync(string ipAddress, CancellationToken ct = default) =>
        db.BlacklistedIps.FirstOrDefaultAsync(b => b.IpAddress == ipAddress && b.ExpiresAt == null, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlacklistedIp>> ListActiveAsync(DateTime now, CancellationToken ct = default) =>
        await db.BlacklistedIps
            .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<BlacklistedIp?> GetAsync(int id, CancellationToken ct = default) =>
        db.BlacklistedIps.FirstOrDefaultAsync(b => b.Id == id, ct);

    /// <inheritdoc />
    public Task AddAsync(BlacklistedIp entry, CancellationToken ct = default)
    {
        db.BlacklistedIps.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove(BlacklistedIp entry) => db.BlacklistedIps.Remove(entry);
}
