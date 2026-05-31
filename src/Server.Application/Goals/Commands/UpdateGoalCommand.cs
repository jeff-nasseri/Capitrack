using Server.Application.Common.Exceptions;
using Server.Application.Goals;
using Server.Application.Tags;

namespace Server.Application.Goals.Commands;

public record UpdateGoalCommand(
    int Id, string? Title, double? TargetAmount, string? TargetDate, string? Description,
    bool? Achieved, int? CategoryId, List<int>? TagIds) : IRequest<GoalDto>;

public sealed class UpdateGoalHandler(IGoalRepository goals, ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<UpdateGoalCommand, GoalDto>
{
    public async Task<GoalDto> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
    {
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
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return goal.ToDto(dtos);
    }
}
