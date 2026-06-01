using Server.Application.Tags;

namespace Server.Application.Goals;

/// <summary>API representation of a goal, including its attached tags.</summary>
/// <param name="Id">The goal's identifier.</param>
/// <param name="Title">The goal's title.</param>
/// <param name="TargetAmount">The target amount.</param>
/// <param name="TargetDate">The target date (yyyy-MM-dd).</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Achieved">1 when achieved, otherwise 0.</param>
/// <param name="CategoryId">The optional owning category's identifier.</param>
/// <param name="CreatedAt">When the goal was created.</param>
/// <param name="UpdatedAt">When the goal was last updated.</param>
/// <param name="Tags">The tags attached to the goal.</param>
public record GoalDto(
    int Id, string Title, double TargetAmount, string TargetDate, string Description,
    int Achieved, int? CategoryId, DateTime CreatedAt, DateTime UpdatedAt, List<TagDto> Tags);
