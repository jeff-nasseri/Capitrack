using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Goals.Commands;

/// <summary>Updates an existing goal and its tag links.</summary>
/// <param name="Id">The goal's identifier.</param>
/// <param name="Title">The new title, or null/empty to keep the current value.</param>
/// <param name="TargetAmount">The new target amount, or null to keep the current value.</param>
/// <param name="TargetDate">The new target date, or null/empty to keep the current value.</param>
/// <param name="Description">The new description, or null to keep the current value.</param>
/// <param name="Achieved">The new achieved flag, or null to keep the current value.</param>
/// <param name="CategoryId">The new category id, or null to keep the current value.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record UpdateGoalCommand(
    int Id, string? Title, double? TargetAmount, string? TargetDate, string? Description,
    bool? Achieved, int? CategoryId, List<int>? TagIds) : IRequest<GoalDto>;

/// <summary>Handles <see cref="UpdateGoalCommand"/>.</summary>
public sealed class UpdateGoalHandler(
    IGoalRepository goals,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpdateGoalHandler> logger)
    : IRequestHandler<UpdateGoalCommand, GoalDto>
{
    /// <summary>Loads, updates and persists the goal, re-links its tags and returns the resulting DTO.</summary>
    public async Task<GoalDto> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpdateGoalCommand));

        var goal = await goals.GetAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Goal not found");

        var title = string.IsNullOrEmpty(request.Title) ? goal.Title : request.Title;
        var targetAmount = request.TargetAmount ?? goal.TargetAmount;
        var targetDate = string.IsNullOrEmpty(request.TargetDate) ? goal.TargetDate.Value : request.TargetDate;
        var description = request.Description ?? goal.Description;
        var achieved = request.Achieved ?? goal.Achieved;
        var categoryId = request.CategoryId ?? goal.CategoryId;

        goal.Update(title, targetAmount, TradeDate.Create(targetDate), description, achieved, categoryId);
        await uow.SaveChangesAsync(cancellationToken);

        await goals.ReplaceTagsAsync(goal.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
        var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<GoalDto>(goal);
        return dto with { Tags = dtos };
    }
}
