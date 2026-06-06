using Server.Domain.Transactions;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for <see cref="Transaction"/> aggregates and their tag links.</summary>
public interface ITransactionRepository
{
    /// <summary>Returns transactions matching the optional account/symbol/search/type filters and paging.</summary>
    Task<IReadOnlyList<Transaction>> ListAsync(int? accountId, string? symbol, string? search, string? type, int? limit, int? offset, CancellationToken ct = default);

    /// <summary>Counts all transactions matching the optional account/symbol/search/type filters, ignoring paging.</summary>
    Task<int> CountAsync(int? accountId, string? symbol, string? search, string? type, CancellationToken ct = default);

    /// <summary>Returns all transactions for a single account.</summary>
    Task<IReadOnlyList<Transaction>> ForAccountAsync(int accountId, CancellationToken ct = default);

    /// <summary>Returns every transaction.</summary>
    Task<IReadOnlyList<Transaction>> AllAsync(CancellationToken ct = default);

    /// <summary>Returns the transaction with the given id, or null.</summary>
    Task<Transaction?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Tracks a new transaction for insertion.</summary>
    Task AddAsync(Transaction transaction, CancellationToken ct = default);

    /// <summary>Marks a transaction for deletion.</summary>
    void Remove(Transaction transaction);

    /// <summary>Returns the tag ids linked to a transaction.</summary>
    Task<IReadOnlyList<int>> TagIdsAsync(int transactionId, CancellationToken ct = default);

    /// <summary>Replaces the transaction's tag links with the given set.</summary>
    Task ReplaceTagsAsync(int transactionId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);
}
