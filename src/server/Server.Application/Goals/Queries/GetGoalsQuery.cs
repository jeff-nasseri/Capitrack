using Server.Application.Tags;

namespace Server.Application.Goals.Queries;

/// <summary>Returns goals matching the optional category/tag filters.</summary>
/// <param name="CategoryId">An optional category filter.</param>
/// <param name="TagId">An optional tag filter.</param>
public record GetGoalsQuery(int? CategoryId, int? TagId) : IRequest<List<GoalDto>>;

/// <summary>Handles <see cref="GetGoalsQuery"/>.</summary>
public sealed class GetGoalsQueryHandler(
    IGoalRepository goals,
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetGoalsQueryHandler> logger)
    : IRequestHandler<GetGoalsQuery, List<GoalDto>>
{
    /// <summary>Loads the matching goals, resolves their tags and returns the resulting DTOs.</summary>
    public async Task<List<GoalDto>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetGoalsQuery));

        var list = await goals.ListAsync(request.CategoryId, request.TagId, cancellationToken);
        var result = new List<GoalDto>(list.Count);
        foreach (var goal in list)
        {
            var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
            var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
            var dtos = goalTags.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();
            var dto = mapper.Map<GoalDto>(goal);
            result.Add(dto with { Tags = dtos });
        }
        return result;
    }
}
