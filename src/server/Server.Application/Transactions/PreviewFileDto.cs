namespace Server.Application.Transactions;

/// <summary>The preview of a single import file: its detected format and parsed (un-imported) rows.</summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Format">The detected CSV format identifier.</param>
/// <param name="Transactions">The parsed transaction rows.</param>
public record PreviewFileDto(string FileName, string Format, List<PreviewTransactionDto> Transactions);
