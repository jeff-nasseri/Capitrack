using Server.Domain.Accounts;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(CapitrackDbContext db) : IAccountRepository
{
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken ct = default) =>
        await db.Accounts.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToListAsync(ct);

    public Task<Account?> GetAsync(int id, CancellationToken ct = default) =>
        db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        db.Accounts.AnyAsync(a => a.Id == id, ct);

    public Task AddAsync(Account account, CancellationToken ct = default)
    {
        db.Accounts.Add(account);
        return Task.CompletedTask;
    }

    public void Remove(Account account) => db.Accounts.Remove(account);

    public async Task<IReadOnlyList<int>> TagIdsAsync(int accountId, CancellationToken ct = default) =>
        await db.AccountTags.Where(t => t.AccountId == accountId).Select(t => t.TagId).ToListAsync(ct);

    public async Task ReplaceTagsAsync(int accountId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default)
    {
        var existing = await db.AccountTags.Where(t => t.AccountId == accountId).ToListAsync(ct);
        db.AccountTags.RemoveRange(existing);
        foreach (var tagId in tagIds.Distinct())
            db.AccountTags.Add(new AccountTag { AccountId = accountId, TagId = tagId });
        await db.SaveChangesAsync(ct);
    }

    public async Task PurgeAllAsync(CancellationToken ct = default)
    {
        await db.TransactionTags.ExecuteDeleteAsync(ct);
        await db.AccountTags.ExecuteDeleteAsync(ct);
        await db.GoalTags.ExecuteDeleteAsync(ct);
        await db.Transactions.ExecuteDeleteAsync(ct);
        await db.Goals.ExecuteDeleteAsync(ct);
        await db.Accounts.ExecuteDeleteAsync(ct);
        await db.PriceCache.ExecuteDeleteAsync(ct);
    }
}
