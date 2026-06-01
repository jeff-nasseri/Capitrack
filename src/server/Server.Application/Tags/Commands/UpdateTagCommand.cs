using Server.Application.Common.Exceptions;

namespace Server.Application.Tags.Commands;

/// <summary>Updates an existing tag's name and color.</summary>
/// <param name="Id">The tag's identifier.</param>
/// <param name="Name">The new name (required, unique).</param>
/// <param name="Color">The new hex color.</param>
public record UpdateTagCommand(int Id, string? Name, string? Color) : IRequest<TagDto>;

/// <summary>Validates <see cref="UpdateTagCommand"/>.</summary>
public sealed class UpdateTagValidator : AbstractValidator<UpdateTagCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public UpdateTagValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tag name is required.");
}

/// <summary>Handles <see cref="UpdateTagCommand"/>.</summary>
public sealed class UpdateTagHandler(
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<UpdateTagHandler> logger)
    : IRequestHandler<UpdateTagCommand, TagDto>
{
    /// <summary>Loads and updates the tag (rejecting name clashes), persists it and returns the resulting DTO.</summary>
    public async Task<TagDto> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(UpdateTagCommand));

        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");

        var name = request.Name!.Trim();
        var clash = await tags.GetByNameAsync(name, cancellationToken);
        if (clash is not null && clash.Id != request.Id)
            throw new ConflictException("A tag with this name already exists.");

        tag.Update(name, Color.CreateOrDefault(request.Color));
        await uow.SaveChangesAsync(cancellationToken);
        return mapper.Map<TagDto>(tag);
    }
}
