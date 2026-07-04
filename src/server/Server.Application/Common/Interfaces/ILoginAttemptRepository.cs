using Server.Domain.Security;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence for sign-in audit records.</summary>
public interface ILoginAttemptRepository
{
    /// <summary>Records a sign-in attempt.</summary>
    Task AddAsync(LoginAttempt attempt, CancellationToken ct = default);

    /// <summary>Counts FAILED attempts from an IP since the given instant (for brute-force detection).</summary>
    Task<int> CountRecentFailuresAsync(string ipAddress, DateTime since, CancellationToken ct = default);

    /// <summary>Returns a most-recent-first page of attempts.</summary>
    Task<IReadOnlyList<LoginAttempt>> ListAsync(int limit, int offset, CancellationToken ct = default);

    /// <summary>Total number of recorded attempts.</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>Deletes attempts older than the cutoff (retention). Returns the number removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
