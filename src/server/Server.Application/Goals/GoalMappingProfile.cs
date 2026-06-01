using Server.Domain.Goals;

namespace Server.Application.Goals;

/// <summary>AutoMapper profile mapping <see cref="Goal"/> aggregates to <see cref="GoalDto"/>.</summary>
public sealed class GoalMappingProfile : Profile
{
    /// <summary>Configures the goal mappings.</summary>
    public GoalMappingProfile()
    {
        CreateMap<Goal, GoalDto>()
            .ForCtorParam("TargetDate", o => o.MapFrom(s => s.TargetDate.Value))
            .ForCtorParam("Achieved", o => o.MapFrom(s => s.Achieved ? 1 : 0))
            .ForCtorParam("Tags", o => o.MapFrom(_ => new List<TagDto>()));
    }
}
