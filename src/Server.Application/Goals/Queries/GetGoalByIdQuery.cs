using Server.Application.Common.Exceptions;
using Server.Application.Goals;
using Server.Application.Tags;

namespace Server.Application.Goals.Queries;

public record GetGoalByIdQuery(int Id) : IRequest<GoalDto>;

public sealed class GetGoalByIdQueryHandler(IGoalRepository goals, ITagRepository tags)
    : IRequestHandler<GetGoalByIdQuery, GoalDto>
{
    public async Task<GoalDto> Handle(GetGoalByIdQuery request, CancellationToken cancellationToken)
    {
        var goal = await goals.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Goal not found");

        var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
        var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return goal.ToDto(dtos);
    }
}
