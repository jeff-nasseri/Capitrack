using Server.Domain.Goals;

namespace Server.Application.Goals;

public static class GoalMappings
{
    public static GoalDto ToDto(this Goal g, List<TagDto> tags) => new(
        g.Id, g.Title, g.TargetAmount, g.TargetDate.Value, g.Description,
        g.Achieved ? 1 : 0, g.CategoryId, g.CreatedAt, g.UpdatedAt, tags);
}
