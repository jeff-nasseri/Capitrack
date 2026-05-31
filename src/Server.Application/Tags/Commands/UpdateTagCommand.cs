using Server.Application.Common.Exceptions;
using Server.Application.Tags;

namespace Server.Application.Tags.Commands;

public record UpdateTagCommand(int Id, string? Name, string? Color) : IRequest<TagDto>;

public sealed class UpdateTagValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagValidator() =>
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tag name is required.");
}

public sealed class UpdateTagHandler(ITagRepository tags, IUnitOfWork uow)
    : IRequestHandler<UpdateTagCommand, TagDto>
{
    public async Task<TagDto> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetAsync(request.Id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found");

        var name = request.Name!.Trim();
        var clash = await tags.GetByNameAsync(name, cancellationToken);
        if (clash is not null && clash.Id != request.Id)
            throw new ConflictException("A tag with this name already exists.");

        tag.Update(name, Color.CreateOrDefault(request.Color));
        await uow.SaveChangesAsync(cancellationToken);
        return tag.ToDto();
    }
}
