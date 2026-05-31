using Server.Domain.Transactions;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(CapitrackDbContext db) : ITransactionRepository
{
    public async Task<IReadOnlyList<Transaction>> ListAsync(int? accountId, string? symbol, int? limit, int? offset, CancellationToken ct = default)
    {
        var q = db.Transactions.AsQueryable();
        if (accountId.HasValue) q = q.Where(t => t.AccountId == accountId.Value);
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var s = Symbol.Create(symbol);
            q = q.Where(t => t.Symbol == s);
        }
        q = q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        return await q.ToListAsync(ct);
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
