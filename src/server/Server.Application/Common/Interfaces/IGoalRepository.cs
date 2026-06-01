using Server.Domain.Goals;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for <see cref="Goal"/> aggregates and their tag links.</summary>
public interface IGoalRepository
{
    /// <summary>Returns goals matching the optional category/tag filters.</summary>
    Task<IReadOnlyList<Goal>> ListAsync(int? categoryId, int? tagId, CancellationToken ct = default);

    /// <summary>Returns the goal with the given id, or null.</summary>
    Task<Goal?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Tracks a new goal for insertion.</summary>
    Task AddAsync(Goal goal, CancellationToken ct = default);

    /// <summary>Marks a goal for deletion.</summary>
    void Remove(Goal goal);

    /// <summary>Removes all goals.</summary>
    Task RemoveAllAsync(CancellationToken ct = default);

    /// <summary>Returns the tag ids linked to a goal.</summary>
    Task<IReadOnlyList<int>> TagIdsAsync(int goalId, CancellationToken ct = default);

    /// <summary>Replaces the goal's tag links with the given set.</summary>
    Task ReplaceTagsAsync(int goalId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);
}
