namespace Server.Application.Settings.Commands;

/// <summary>Destructively replaces all portfolio data with the supplied snapshot.</summary>
/// <param name="Snapshot">The snapshot whose contents become the new dataset.</param>
public record ImportDatabaseCommand(DatabaseSnapshot Snapshot) : IRequest<ImportResult>;

/// <summary>Handles <see cref="ImportDatabaseCommand"/>.</summary>
public sealed class ImportDatabaseCommandHandler(
    IDatabaseBackupService backup,
    ILogger<ImportDatabaseCommandHandler> logger)
    : IRequestHandler<ImportDatabaseCommand, ImportResult>
{
    /// <summary>Replaces every portfolio table with the snapshot and returns the imported counts.</summary>
    public async Task<ImportResult> Handle(ImportDatabaseCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(ImportDatabaseCommand));
        return await backup.ImportAsync(request.Snapshot, cancellationToken);
    }
}
