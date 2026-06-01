namespace Server.Application.Common.Interfaces;

/// <summary>Commits tracked changes from the repositories in a single transaction.</summary>
public interface IUnitOfWork
{
    /// <summary>Persists all tracked changes and returns the number of affected rows.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
