using Server.Application.Common.Exceptions;
using Server.Domain.Tags;

namespace Server.Application.Tags.Commands;

/// <summary>Creates a new tag.</summary>
/// <param name="Name">The tag name (required, unique).</param>
/// <param name="Color">The tag's hex color.</param>
public record CreateTagCommand(string? Name, string? Color) : IRequest<TagDto>;

/// <summary>Validates <see cref="CreateTagCommand"/>.</summary>
public sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public CreateTagValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tag name is required.");
}

/// <summary>Handles <see cref="CreateTagCommand"/>.</summary>
public sealed class CreateTagHandler(
    ITagRepository tags,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<CreateTagHandler> logger)
    : IRequestHandler<CreateTagCommand, TagDto>
{
    /// <summary>Creates the tag (rejecting duplicates), persists it and returns the resulting DTO.</summary>
    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(CreateTagCommand));

        var name = request.Name!.Trim();
        if (await tags.GetByNameAsync(name, cancellationToken) is not null)
            throw new ConflictException("A tag with this name already exists.");

        var tag = Tag.Create(name, Color.CreateOrDefault(request.Color));
        await tags.AddAsync(tag, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return mapper.Map<TagDto>(tag);
    }
}
