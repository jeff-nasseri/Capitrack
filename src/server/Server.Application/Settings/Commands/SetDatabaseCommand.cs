namespace Server.Application.Settings.Commands;

/// <summary>Changes the active database path.</summary>
/// <param name="Path">The new database file path (required).</param>
public record SetDatabaseCommand(string? Path) : IRequest<DatabaseUpdateDto>;

/// <summary>Validates <see cref="SetDatabaseCommand"/>.</summary>
public sealed class SetDatabaseValidator : AbstractValidator<SetDatabaseCommand>
{
    /// <summary>Configures the validation rules.</summary>
    public SetDatabaseValidator() =>
        RuleFor(x => x.Path).NotEmpty().WithMessage("Database path required");
}

/// <summary>Handles <see cref="SetDatabaseCommand"/>.</summary>
public sealed class SetDatabaseHandler(
    ISystemService system,
    ILogger<SetDatabaseHandler> logger)
    : IRequestHandler<SetDatabaseCommand, DatabaseUpdateDto>
{
    /// <summary>Applies the new database path via the system service and returns the result.</summary>
    public Task<DatabaseUpdateDto> Handle(SetDatabaseCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(SetDatabaseCommand));

        var (message, path, exists) = system.SetDatabasePath(request.Path!);
        return Task.FromResult(new DatabaseUpdateDto(message, path, exists));
    }
}
