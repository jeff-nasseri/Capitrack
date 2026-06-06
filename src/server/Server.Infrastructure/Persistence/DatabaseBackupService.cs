using Server.Domain.Accounts;
using Server.Domain.Currencies;
using Server.Domain.Goals;
using Server.Domain.Tags;
using Server.Domain.Transactions;

namespace Server.Infrastructure.Persistence;

/// <summary>
/// Exports the full portfolio dataset to a <see cref="DatabaseSnapshot"/> and restores it.
/// Import is a destructive, transactional full-replace that preserves the user/auth rows and
/// clears the transient price cache. Primary keys are REMAPPED on import (EF + SQLite make
/// explicit store-generated-key inserts unreliable), so every account/tag/goal link stays
/// valid; created_at/updated_at are reset to import time.
/// </summary>
public sealed class DatabaseBackupService(CapitrackDbContext db) : IDatabaseBackupService
{
    private const int SnapshotVersion = 1;

    /// <inheritdoc />
    public async Task<DatabaseSnapshot> ExportAsync(CancellationToken ct)
    {
        var accounts = await db.Accounts.AsNoTracking().ToListAsync(ct);
        var transactions = await db.Transactions.AsNoTracking().ToListAsync(ct);
        var tags = await db.Tags.AsNoTracking().ToListAsync(ct);
        var goals = await db.Goals.AsNoTracking().ToListAsync(ct);
        var rates = await db.CurrencyRates.AsNoTracking().ToListAsync(ct);
        var dailyWealth = await db.DailyWealth.AsNoTracking().ToListAsync(ct);

        var accountTags = (await db.AccountTags.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.AccountId).ToDictionary(g => g.Key, g => g.Select(x => x.TagId).ToList());
        var transactionTags = (await db.TransactionTags.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.TransactionId).ToDictionary(g => g.Key, g => g.Select(x => x.TagId).ToList());
        var goalTags = (await db.GoalTags.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.GoalId).ToDictionary(g => g.Key, g => g.Select(x => x.TagId).ToList());

        return new DatabaseSnapshot(
            SnapshotVersion,
            DateTime.UtcNow,
            accounts.Select(a => new SnapshotAccount(
                a.Id, a.Name, a.Type.Value, a.Currency.Value, a.Description, a.Icon, a.Color.Value,
                accountTags.GetValueOrDefault(a.Id, []))).ToList(),
            transactions.Select(t => new SnapshotTransaction(
                t.Id, t.AccountId, t.Symbol.Value, t.Type.Value, t.Quantity.Value, t.Price, t.Fee,
                t.Currency.Value, t.Date.Value, t.Notes, transactionTags.GetValueOrDefault(t.Id, []))).ToList(),
            tags.Select(t => new SnapshotTag(t.Id, t.Name, t.Color.Value)).ToList(),
            goals.Select(g => new SnapshotGoal(
                g.Id, g.Title, g.TargetAmount, g.TargetDate.Value, g.Description, g.Achieved, g.CategoryId,
                goalTags.GetValueOrDefault(g.Id, []))).ToList(),
            rates.Select(r => new SnapshotCurrencyRate(r.Id, r.FromCurrency.Value, r.ToCurrency.Value, r.Rate)).ToList(),
            dailyWealth.Select(d => new SnapshotDailyWealth(d.Date, d.TotalWealth, d.TotalCost, d.BaseCurrency, d.Details)).ToList());
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportAsync(DatabaseSnapshot snapshot, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Wipe every portfolio table. Users (auth) are preserved; the price cache is transient.
            await db.TransactionTags.ExecuteDeleteAsync(ct);
            await db.GoalTags.ExecuteDeleteAsync(ct);
            await db.AccountTags.ExecuteDeleteAsync(ct);
            await db.Transactions.ExecuteDeleteAsync(ct);
            await db.Goals.ExecuteDeleteAsync(ct);
            await db.CurrencyRates.ExecuteDeleteAsync(ct);
            await db.DailyWealth.ExecuteDeleteAsync(ct);
            await db.Accounts.ExecuteDeleteAsync(ct);
            await db.Tags.ExecuteDeleteAsync(ct);
            await db.PriceCache.ExecuteDeleteAsync(ct);

            // 2. Tags first (everything else may reference them).
            var tagPairs = (snapshot.Tags ?? [])
                .Select(t => (Src: t, Entity: Tag.Create(t.Name, Color.CreateOrDefault(t.Color))))
                .ToList();
            db.Tags.AddRange(tagPairs.Select(p => p.Entity));
            await db.SaveChangesAsync(ct);
            var tagMap = tagPairs.ToDictionary(p => p.Src.Id, p => p.Entity.Id);

            // 3. Accounts + their tag links.
            var accountPairs = (snapshot.Accounts ?? [])
                .Select(a => (Src: a, Entity: Account.Create(
                    a.Name, AccountType.From(a.Type), CurrencyCode.Create(a.Currency),
                    a.Description, a.Icon, Color.CreateOrDefault(a.Color))))
                .ToList();
            db.Accounts.AddRange(accountPairs.Select(p => p.Entity));
            await db.SaveChangesAsync(ct);
            var accountMap = accountPairs.ToDictionary(p => p.Src.Id, p => p.Entity.Id);
            foreach (var (src, entity) in accountPairs)
                AddTagLinks(src.TagIds, tagMap, newTagId => db.AccountTags.Add(new AccountTag { AccountId = entity.Id, TagId = newTagId }));

            // 4. Goals + their tag links (categories are unused, so category_id is dropped).
            var goalPairs = (snapshot.Goals ?? [])
                .Select(g => (Src: g, Entity: Goal.Create(
                    g.Title, g.TargetAmount, TradeDate.Create(g.TargetDate), g.Description, g.Achieved, null)))
                .ToList();
            db.Goals.AddRange(goalPairs.Select(p => p.Entity));
            await db.SaveChangesAsync(ct);
            foreach (var (src, entity) in goalPairs)
                AddTagLinks(src.TagIds, tagMap, newTagId => db.GoalTags.Add(new GoalTag { GoalId = entity.Id, TagId = newTagId }));

            // 5. Transactions (account ids remapped; orphans skipped) + their tag links.
            var transactionPairs = (snapshot.Transactions ?? [])
                .Where(t => accountMap.ContainsKey(t.AccountId))
                .Select(t => (Src: t, Entity: Transaction.Create(
                    accountMap[t.AccountId], Symbol.Create(t.Symbol), TransactionType.From(t.Type),
                    Quantity.Create(t.Quantity), t.Price, t.Fee, CurrencyCode.Create(t.Currency),
                    TradeDate.Create(t.Date), t.Notes)))
                .ToList();
            db.Transactions.AddRange(transactionPairs.Select(p => p.Entity));
            await db.SaveChangesAsync(ct);
            foreach (var (src, entity) in transactionPairs)
                AddTagLinks(src.TagIds, tagMap, newTagId => db.TransactionTags.Add(new TransactionTag { TransactionId = entity.Id, TagId = newTagId }));

            // 6. Currency rates + daily wealth snapshots (no foreign keys).
            db.CurrencyRates.AddRange((snapshot.CurrencyRates ?? [])
                .Select(r => CurrencyRate.Create(CurrencyCode.Create(r.FromCurrency), CurrencyCode.Create(r.ToCurrency), r.Rate)));
            db.DailyWealth.AddRange((snapshot.DailyWealth ?? [])
                .Select(d => new DailyWealthRecord
                {
                    Date = d.Date, TotalWealth = d.Total, TotalCost = d.TotalCost,
                    BaseCurrency = d.BaseCurrency, Details = string.IsNullOrEmpty(d.Details) ? "{}" : d.Details
                }));
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            var rateCount = snapshot.CurrencyRates?.Count ?? 0;
            var wealthCount = snapshot.DailyWealth?.Count ?? 0;
            var message = $"Replaced all data: {accountMap.Count} accounts, {transactionPairs.Count} transactions, " +
                          $"{tagMap.Count} tags, {goalPairs.Count} goals, {rateCount} currency rates.";
            return new ImportResult(accountMap.Count, transactionPairs.Count, tagMap.Count, goalPairs.Count, rateCount, wealthCount, message);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>Adds a join-table row for each known tag id, skipping ids absent from the import.</summary>
    private static void AddTagLinks(List<int>? tagIds, IReadOnlyDictionary<int, int> tagMap, Action<int> add)
    {
        foreach (var oldTagId in tagIds ?? [])
            if (tagMap.TryGetValue(oldTagId, out var newTagId))
                add(newTagId);
    }
}
