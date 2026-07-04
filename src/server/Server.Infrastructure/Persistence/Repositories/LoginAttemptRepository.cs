using Server.Domain.Security;

namespace Server.Infrastructure.Persistence.Repositories;

/// <summary>EF Core persistence for <see cref="LoginAttempt"/> audit records.</summary>
public sealed class LoginAttemptRepository(CapitrackDbContext db) : ILoginAttemptRepository
{
    /// <inheritdoc />
    public Task AddAsync(LoginAttempt attempt, CancellationToken ct = default)
    {
        db.LoginAttempts.Add(attempt);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> CountRecentFailuresAsync(string ipAddress, DateTime since, CancellationToken ct = default) =>
        db.LoginAttempts.CountAsync(
            a => a.IpAddress == ipAddress && !a.Success && a.Reason != "ip_blocked" && a.CreatedAt >= since, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoginAttempt>> ListAsync(int limit, int offset, CancellationToken ct = default) =>
        await db.LoginAttempts
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(offset).Take(limit)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken ct = default) => db.LoginAttempts.CountAsync(ct);

    /// <inheritdoc />
    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default) =>
        db.LoginAttempts.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
}
