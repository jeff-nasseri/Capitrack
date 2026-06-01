namespace Server.Application.Tags.Queries;

/// <summary>Returns all tags.</summary>
public record GetTagsQuery : IRequest<List<TagDto>>;

/// <summary>Handles <see cref="GetTagsQuery"/>.</summary>
public sealed class GetTagsQueryHandler(
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetTagsQueryHandler> logger)
    : IRequestHandler<GetTagsQuery, List<TagDto>>
{
    /// <summary>Loads all tags and returns the resulting DTOs.</summary>
    public async Task<List<TagDto>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetTagsQuery));

        var all = await tags.ListAsync(cancellationToken);
        return all.Select(t => mapper.Map<TagDto>(t)).ToList();
    }
}
