namespace Server.Application.Settings;

/// <summary>A single goal inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Id">The goal's original identifier (remapped on import).</param>
/// <param name="Title">The goal's title.</param>
/// <param name="TargetAmount">The target amount to reach.</param>
/// <param name="TargetDate">The due date (yyyy-MM-dd).</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Achieved">Whether the goal has been achieved.</param>
/// <param name="CategoryId">The optional owning category's identifier (left null on import).</param>
/// <param name="TagIds">The original tag ids linked to the goal (remapped on import).</param>
public record SnapshotGoal(
    int Id,
    string Title,
    double TargetAmount,
    string TargetDate,
    string? Description,
    bool Achieved,
    int? CategoryId,
    List<int> TagIds);
