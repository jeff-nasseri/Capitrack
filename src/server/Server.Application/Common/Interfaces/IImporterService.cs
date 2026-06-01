namespace Server.Application.Common.Interfaces;

/// <summary>CSV format detection + multi-format transaction import with de-duplication.</summary>
public interface IImporterService
{
    /// <summary>Detects the CSV format of the given content.</summary>
    DetectResultDto Detect(string content);

    /// <summary>Imports transactions from CSV content into an account, optionally with a format hint.</summary>
    Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint);
}
