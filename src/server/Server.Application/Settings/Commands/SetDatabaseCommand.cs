using Server.Application.Settings;

namespace Server.Application.Settings.Commands;

public record SetDatabaseCommand(string? Path) : IRequest<DatabaseUpdateDto>;

public sealed class SetDatabaseValidator : AbstractValidator<SetDatabaseCommand>
{
    public SetDatabaseValidator() =>
        RuleFor(x => x.Path).NotEmpty().WithMessage("Database path required");
}

public sealed class SetDatabaseHandler(ISystemService system)
    : IRequestHandler<SetDatabaseCommand, DatabaseUpdateDto>
{
    public Task<DatabaseUpdateDto> Handle(SetDatabaseCommand request, CancellationToken cancellationToken)
    {
        var (message, path, exists) = system.SetDatabasePath(request.Path!);
        return Task.FromResult(new DatabaseUpdateDto(message, path, exists));
    }
}
