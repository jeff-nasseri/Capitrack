using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Tags.Queries;

public record GetTagByIdQuery(int Id) : IRequest<TagDto>;

public sealed class GetTagByIdQueryHandler(ITagRepository tags) : IRequestHandler<GetTagByIdQuery, TagDto>
{
    public async Task<TagDto> Handle(GetTagByIdQuery request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");
        return tag.ToDto();
    }
}
