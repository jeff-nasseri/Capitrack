using Server.Domain.Tags;

namespace Server.Application.Tags;

public static class TagMappings
{
    public static TagDto ToDto(this Tag tag) => new(tag.Id, tag.Name, tag.Color.Value, tag.CreatedAt);
}
