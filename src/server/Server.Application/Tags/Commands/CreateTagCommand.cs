using Server.Application.Common.Exceptions;
using Server.Application.Tags;
using Server.Domain.Tags;

namespace Server.Application.Tags.Commands;

public record CreateTagCommand(string? Name, string? Color) : IRequest<TagDto>;

public sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tag name is required.");
}

public sealed class CreateTagHandler(ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<CreateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name!.Trim();
        if (await tags.GetByNameAsync(name, cancellationToken) is not null)
            throw new ConflictException("A tag with this name already exists.");

        var tag = Tag.Create(name, Color.CreateOrDefault(request.Color));
        await tags.AddAsync(tag, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return tag.ToDto();
    }
}
