using Server.Domain.Tags;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class TagRepository(CapitrackDbContext db) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default) =>
        await db.Tags.OrderBy(t => t.Name).ToListAsync(ct);

    public Task<Tag?> GetAsync(int id, CancellationToken ct = default) =>
        db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tag?> GetByNameAsync(string name, CancellationToken ct = default) =>
        db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task<IReadOnlyList<Tag>> ByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var set = ids.ToHashSet();
        return await db.Tags.Where(t => set.Contains(t.Id)).ToListAsync(ct);
    }

    public Task AddAsync(Tag tag, CancellationToken ct = default)
    {
        db.Tags.Add(tag);
        return Task.CompletedTask;
    }

    public void Remove(Tag tag) => db.Tags.Remove(tag);
}
