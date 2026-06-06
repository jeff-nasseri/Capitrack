namespace Server.Application.Common.Interfaces;

/// <summary>
/// Exports all portfolio data to, and restores it from, a <see cref="DatabaseSnapshot"/>.
/// Import is a destructive full replace of every portfolio table (auth rows are preserved).
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>Reads every portfolio table into a single self-contained snapshot.</summary>
    Task<DatabaseSnapshot> ExportAsync(CancellationToken ct);

    /// <summary>
    /// Destructively replaces all portfolio data with the contents of <paramref name="snapshot"/>.
    /// Returns the per-entity counts actually imported.
    /// </summary>
    Task<ImportResult> ImportAsync(DatabaseSnapshot snapshot, CancellationToken ct);
}
