using Server.Application.Tags;
using Server.Domain.Goals;

namespace Server.Application.Goals.Commands;

/// <summary>Creates a new goal with optional tag links.</summary>
/// <param name="Title">The goal title (required).</param>
/// <param name="TargetAmount">The target amount.</param>
/// <param name="TargetDate">The target date (required, yyyy-MM-dd).</param>
/// <param name="Description">A free-text description.</param>
/// <param name="Achieved">Whether the goal is already achieved.</param>
/// <param name="CategoryId">The optional owning category's identifier.</param>
/// <param name="TagIds">The ids of tags to attach.</param>
public record CreateGoalCommand(
    string? Title, double? TargetAmount, string? TargetDate, string? Description,
    bool? Achieved, int? CategoryId, List<int>? TagIds) : IRequest<GoalDto>;

/// <summary>Validates <see cref="CreateGoalCommand"/>.</summary>
public sealed class CreateGoalValidator : AbstractValidator<CreateGoalCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateGoalValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title and target date required");
        RuleFor(x => x.TargetDate).NotEmpty().WithMessage("Title and target date required");
    }
}

/// <summary>Handles <see cref="CreateGoalCommand"/>.</summary>
public sealed class CreateGoalHandler(
    IGoalRepository goals,
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<CreateGoalHandler> logger)
    : IRequestHandler<CreateGoalCommand, GoalDto>
{
    /// <summary>Creates the goal, persists it, links its tags and returns the resulting DTO.</summary>
    public async Task<GoalDto> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(CreateGoalCommand));

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
        var dtos = goalTags.OrderBy(t => t.Name).Select(t => mapper.Map<TagDto>(t)).ToList();

        var dto = mapper.Map<GoalDto>(goal);
        return dto with { Tags = dtos };
    }
}
