using Server.Application.Goals;
using Server.Application.Tags;

namespace Server.Application.Goals.Queries;

public record GetGoalsQuery(int? CategoryId, int? TagId) : IRequest<List<GoalDto>>;

public sealed class GetGoalsQueryHandler(IGoalRepository goals, ITagRepository tags)
    : IRequestHandler<GetGoalsQuery, List<GoalDto>>
{
    public async Task<List<GoalDto>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        var list = await goals.ListAsync(request.CategoryId, request.TagId, cancellationToken);
        var result = new List<GoalDto>(list.Count);
        foreach (var goal in list)
        {
            var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
            var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
            var dtos = goalTags.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
            result.Add(goal.ToDto(dtos));
        }
        return result;
    }
}
