using Server.Application.Common.Exceptions;

namespace Server.Application.Tags.Queries;

/// <summary>Returns a single tag by id.</summary>
/// <param name="Id">The tag's identifier.</param>
public record GetTagByIdQuery(int Id) : IRequest<TagDto>;

/// <summary>Handles <see cref="GetTagByIdQuery"/>.</summary>
public sealed class GetTagByIdQueryHandler(
    ITagRepository tags,
    IMapper mapper,
    ILogger<GetTagByIdQueryHandler> logger)
    : IRequestHandler<GetTagByIdQuery, TagDto>
{
    /// <summary>Loads the tag and returns the resulting DTO.</summary>
    public async Task<TagDto> Handle(GetTagByIdQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetTagByIdQuery));

        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");
        return mapper.Map<TagDto>(tag);
    }
}
