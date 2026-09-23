namespace Server.Application.Transactions;

/// <summary>The preview of a single import file: its detected format and parsed (un-imported) rows.</summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Format">The detected CSV format identifier.</param>
/// <param name="Transactions">The parsed transaction rows.</param>
/// <param name="Rejected">Rows that will not be imported, with the reason.</param>
public record PreviewFileDto(string FileName, string Format, List<PreviewTransactionDto> Transactions, List<RejectedRowDto> Rejected);

/// <summary>A CSV data row that will not be imported.</summary>
/// <param name="Row">The 1-based data row number within the file.</param>
/// <param name="Reason">Why the row is not imported.</param>
public record RejectedRowDto(int Row, string Reason);
