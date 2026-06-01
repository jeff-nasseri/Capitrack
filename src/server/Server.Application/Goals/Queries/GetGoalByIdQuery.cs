using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Goals.Queries;

/// <summary>Returns a single goal with its attached tags.</summary>
/// <param name="Id">The goal's identifier.</param>
public record GetGoalByIdQuery(int Id) : IRequest<GoalDto>;

/// <summary>Handles <see cref="GetGoalByIdQuery"/>.</summary>
public sealed class GetGoalByIdQueryHandler(
    IGoalRepository goals,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetGoalByIdQueryHandler> logger)
    : IRequestHandler<GetGoalByIdQuery, GoalDto>
{
    /// <summary>Loads the goal, resolves its tags and returns the resulting DTO.</summary>
    public async Task<GoalDto> Handle(GetGoalByIdQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetGoalByIdQuery));

        var goal = await goals.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Goal not found");

        var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
        var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<GoalDto>(goal);
        return dto with { Tags = dtos };
    }
}
