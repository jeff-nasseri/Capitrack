namespace Server.Application.Settings.Queries;

/// <summary>Produces a full <see cref="DatabaseSnapshot"/> of every portfolio table.</summary>
public record ExportDatabaseQuery : IRequest<DatabaseSnapshot>;

/// <summary>Handles <see cref="ExportDatabaseQuery"/>.</summary>
public sealed class ExportDatabaseQueryHandler(
    IDatabaseBackupService backup,
    ILogger<ExportDatabaseQueryHandler> logger)
    : IRequestHandler<ExportDatabaseQuery, DatabaseSnapshot>
{
    /// <summary>Reads the full portfolio dataset into a single snapshot.</summary>
    public async Task<DatabaseSnapshot> Handle(ExportDatabaseQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ExportDatabaseQuery));
        return await backup.ExportAsync(cancellationToken);
    }
}
