namespace Server.Application.Transactions;

/// <summary>The outcome of a CSV transaction import.</summary>
/// <param name="Imported">The number of transactions imported.</param>
/// <param name="Skipped">The number of duplicate/invalid rows skipped.</param>
/// <param name="Total">The total number of rows processed.</param>
/// <param name="Errors">Per-row error messages.</param>
/// <param name="Format">The detected/used CSV format.</param>
public record ImportResultDto(int Imported, int Skipped, int Total, List<string> Errors, string Format);
