namespace Server.Application.Settings.Queries;

/// <summary>Returns the current database path and existence flag.</summary>
public record GetDatabaseQuery : IRequest<DatabaseInfoDto>;

/// <summary>Handles <see cref="GetDatabaseQuery"/>.</summary>
public sealed class GetDatabaseQueryHandler(
    ISystemService system,
    ILogger<GetDatabaseQueryHandler> logger)
    : IRequestHandler<GetDatabaseQuery, DatabaseInfoDto>
{
    /// <summary>Reads the database path and existence from the system service.</summary>
    public Task<DatabaseInfoDto> Handle(GetDatabaseQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetDatabaseQuery));
        return Task.FromResult(new DatabaseInfoDto(system.DbPath, system.DatabaseExists()));
    }
}
