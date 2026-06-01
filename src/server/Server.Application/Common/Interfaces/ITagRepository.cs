using Server.Domain.Tags;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for <see cref="Tag"/> aggregates.</summary>
public interface ITagRepository
{
    /// <summary>Returns all tags.</summary>
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns the tag with the given id, or null.</summary>
    Task<Tag?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Returns the tag with the given name, or null.</summary>
    Task<Tag?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Returns the tags with the given ids.</summary>
    Task<IReadOnlyList<Tag>> ByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);

    /// <summary>Tracks a new tag for insertion.</summary>
    Task AddAsync(Tag tag, CancellationToken ct = default);

    /// <summary>Marks a tag for deletion.</summary>
    void Remove(Tag tag);
}
