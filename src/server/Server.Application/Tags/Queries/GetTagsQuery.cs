using Server.Application.Tags;

namespace Server.Application.Tags.Queries;

public record GetTagsQuery : IRequest<List<TagDto>>;

public sealed class GetTagsQueryHandler(ITagRepository tags) : IRequestHandler<GetTagsQuery, List<TagDto>>
{
    public async Task<List<TagDto>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var all = await tags.ListAsync(cancellationToken);
        return all.Select(t => t.ToDto()).ToList();
    }
}
