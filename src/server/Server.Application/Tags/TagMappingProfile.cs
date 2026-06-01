using Server.Domain.Tags;

namespace Server.Application.Tags;

/// <summary>AutoMapper profile mapping <see cref="Tag"/> aggregates to <see cref="TagDto"/>.</summary>
public sealed class TagMappingProfile : Profile
{
    /// <summary>Configures the tag mappings.</summary>
    public TagMappingProfile()
    {
        CreateMap<Tag, TagDto>()
            .ForCtorParam("Color", o => o.MapFrom(s => s.Color.Value));
    }
}
