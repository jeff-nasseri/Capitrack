namespace Server.Application.Common.Interfaces;

/// <summary>CSV format detection + multi-format transaction import with de-duplication.</summary>
public interface IImporterService
{
    /// <summary>Detects the CSV format of the given content.</summary>
    DetectResultDto Detect(string content);

    /// <summary>Imports transactions from CSV content into an account, optionally with a format hint.</summary>
    Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint);

    /// <summary>
    /// Parses (without importing) CSV content for an account and returns the detected format plus the
    /// parsed rows, each flagged for whether it duplicates an existing transaction and whether it can be staked.
    /// </summary>
    Task<PreviewFileDto> PreviewAsync(string fileName, string content, int accountId);

    /// <summary>Imports a set of user-selected transactions directly, applying the same fingerprint dedup against existing rows.</summary>
    Task<ImportResultDto> ImportSelectedAsync(int accountId, IEnumerable<SelectedTransactionDto> transactions);
}
