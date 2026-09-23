namespace Server.Application.Common.Interfaces;

/// <summary>One uploaded import file and, optionally, which of its rows to take.</summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Content">The raw CSV content.</param>
/// <param name="Selection">The rows to import and which outflows are staked; null = every importable row, none staked.</param>
public record ImportFileInput(string FileName, string Content, FileSelectionDto? Selection = null);

/// <summary>CSV format detection and idempotent multi-format transaction import.</summary>
public interface IImporterService
{
    /// <summary>Detects the CSV format of the given content.</summary>
    DetectResultDto Detect(string content);

    /// <summary>Imports every importable row of one CSV into an account.</summary>
    Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint);

    /// <summary>
    /// Works out — without writing — what importing the files (with their selections) would do:
    /// each row's status and legs, and per-file reconciliation with the resulting balances.
    /// </summary>
    Task<ImportPreviewDto> PreviewAsync(int accountId, IReadOnlyList<ImportFileInput> files);

    /// <summary>Imports the files (with their selections) atomically, re-parsing them with the preview's exact plan.</summary>
    Task<ImportResultDto> ImportFilesAsync(int accountId, IReadOnlyList<ImportFileInput> files);
}
