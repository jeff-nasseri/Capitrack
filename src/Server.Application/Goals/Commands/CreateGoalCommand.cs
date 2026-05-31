using Server.Application.Goals;
using Server.Application.Tags;
using Server.Domain.Goals;

namespace Server.Application.Goals.Commands;

public record CreateGoalCommand(
    string? Title, double? TargetAmount, string? TargetDate, string? Description,
    bool? Achieved, int? CategoryId, List<int>? TagIds) : IRequest<GoalDto>;

public sealed class CreateGoalValidator : AbstractValidator<CreateGoalCommand>
{
    public CreateGoalValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title and target date required");
        RuleFor(x => x.TargetDate).NotEmpty().WithMessage("Title and target date required");
    }
}

public sealed class CreateGoalHandler(IGoalRepository goals, ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<CreateGoalCommand, GoalDto>
{
    public async Task<GoalDto> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = Goal.Create(
            request.Title,
            request.TargetAmount ?? 0,
            TradeDate.Create(request.TargetDate),
            request.Description,
            request.Achieved ?? false,
            request.CategoryId);

        await goals.AddAsync(goal, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        await goals.ReplaceTagsAsync(goal.Id, request.TagIds ?? new List<int>(), cancellationToken);

        var ids = await goals.TagIdsAsync(goal.Id, cancellationToken);
        var goalTags = await tags.ByIdsAsync(ids, cancellationToken);
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => t.ToDto()).ToList();
        return goal.ToDto(dtos);
    }
}
