using Server.Domain.Accounts;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for <see cref="Account"/> aggregates and their tag links.</summary>
public interface IAccountRepository
{
    /// <summary>Returns all accounts.</summary>
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns the account with the given id, or null.</summary>
    Task<Account?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Returns true when an account with the given id exists.</summary>
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);

    /// <summary>Tracks a new account for insertion.</summary>
    Task AddAsync(Account account, CancellationToken ct = default);

    /// <summary>Marks an account for deletion.</summary>
    void Remove(Account account);

    /// <summary>Returns the tag ids linked to an account.</summary>
    Task<IReadOnlyList<int>> TagIdsAsync(int accountId, CancellationToken ct = default);

    /// <summary>Replaces the account's tag links with the given set.</summary>
    Task ReplaceTagsAsync(int accountId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);

    /// <summary>Removes all accounts (and their dependent data).</summary>
    Task PurgeAllAsync(CancellationToken ct = default);
}
