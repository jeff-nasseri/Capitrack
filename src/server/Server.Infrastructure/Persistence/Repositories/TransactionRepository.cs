using Server.Domain.Transactions;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(CapitrackDbContext db) : ITransactionRepository
{
    public async Task<IReadOnlyList<Transaction>> ListAsync(int? accountId, string? symbol, string? search, string? type, int? limit, int? offset, CancellationToken ct = default)
    {
        IQueryable<Transaction> q = ApplyFilters(db.Transactions.AsQueryable(), accountId, symbol, type)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // A CONTAINS on the symbol can't be translated through the value-object
            // converter, so the search term is applied in memory — consistently with
            // CountAsync, so the page and its total always agree.
            var needle = search.Trim();
            IEnumerable<Transaction> matches = (await q.ToListAsync(ct))
                .Where(t => t.Symbol.Value.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (offset is > 0) matches = matches.Skip(offset.Value);
            if (limit is > 0) matches = matches.Take(limit.Value);
            return matches.ToList();
        }

        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        return await q.ToListAsync(ct);
    }

    public async Task<int> CountAsync(int? accountId, string? symbol, string? search, string? type, CancellationToken ct = default)
    {
        var q = ApplyFilters(db.Transactions.AsQueryable(), accountId, symbol, type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            return (await q.ToListAsync(ct))
                .Count(t => t.Symbol.Value.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }
        return await q.CountAsync(ct);
    }

    /// <summary>
    /// Applies the SQL-translatable account/exact-symbol/type filters shared by
    /// <see cref="ListAsync"/> and <see cref="CountAsync"/>. The free-text symbol
    /// <c>search</c> (CONTAINS) is applied in memory by the callers.
    /// </summary>
    private static IQueryable<Transaction> ApplyFilters(IQueryable<Transaction> q, int? accountId, string? symbol, string? type)
    {
        if (accountId.HasValue) q = q.Where(t => t.AccountId == accountId.Value);
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var s = Symbol.Create(symbol);
            q = q.Where(t => t.Symbol == s);
        }
        if (!string.IsNullOrWhiteSpace(type))
        {
            // Exact match on the persisted TransactionType string (e.g. "buy").
            var t = TransactionType.From(type);
            q = q.Where(x => x.Type == t);
        }
        return q;
    }

    public async Task<IReadOnlyList<Transaction>> ForAccountAsync(int accountId, CancellationToken ct = default) =>
        await db.Transactions.Where(t => t.AccountId == accountId).ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> AllAsync(CancellationToken ct = default) =>
        await db.Transactions.ToListAsync(ct);

    public Task<Transaction?> GetAsync(int id, CancellationToken ct = default) =>
        db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        db.Transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public void Remove(Transaction transaction) => db.Transactions.Remove(transaction);

    public async Task<IReadOnlyList<int>> TagIdsAsync(int transactionId, CancellationToken ct = default) =>
        await db.TransactionTags.Where(t => t.TransactionId == transactionId).Select(t => t.TagId).ToListAsync(ct);

    public async Task ReplaceTagsAsync(int transactionId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default)
    {
        var existing = await db.TransactionTags.Where(t => t.TransactionId == transactionId).ToListAsync(ct);
        db.TransactionTags.RemoveRange(existing);
        foreach (var tagId in tagIds.Distinct())
            db.TransactionTags.Add(new TransactionTag { TransactionId = transactionId, TagId = tagId });
        await db.SaveChangesAsync(ct);
    }
}
