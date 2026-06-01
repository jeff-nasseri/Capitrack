using Server.Domain.Goals;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class GoalRepository(CapitrackDbContext db) : IGoalRepository
{
    public async Task<IReadOnlyList<Goal>> ListAsync(int? categoryId, int? tagId, CancellationToken ct = default)
    {
        var q = db.Goals.AsQueryable();
        if (categoryId.HasValue) q = q.Where(g => g.CategoryId == categoryId.Value);
        if (tagId.HasValue) q = q.Where(g => db.GoalTags.Any(gt => gt.GoalId == g.Id && gt.TagId == tagId.Value));
        return await q.OrderBy(g => g.TargetDate).ToListAsync(ct);
    }

    public Task<Goal?> GetAsync(int id, CancellationToken ct = default) =>
        db.Goals.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task AddAsync(Goal goal, CancellationToken ct = default)
    {
        db.Goals.Add(goal);
        return Task.CompletedTask;
    }

    public void Remove(Goal goal) => db.Goals.Remove(goal);

    public async Task RemoveAllAsync(CancellationToken ct = default)
    {
        await db.GoalTags.ExecuteDeleteAsync(ct);
        await db.Goals.ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<int>> TagIdsAsync(int goalId, CancellationToken ct = default) =>
        await db.GoalTags.Where(t => t.GoalId == goalId).Select(t => t.TagId).ToListAsync(ct);

    public async Task ReplaceTagsAsync(int goalId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default)
    {
        var existing = await db.GoalTags.Where(t => t.GoalId == goalId).ToListAsync(ct);
        db.GoalTags.RemoveRange(existing);
        foreach (var tagId in tagIds.Distinct())
            db.GoalTags.Add(new GoalTag { GoalId = goalId, TagId = tagId });
        await db.SaveChangesAsync(ct);
    }
}
