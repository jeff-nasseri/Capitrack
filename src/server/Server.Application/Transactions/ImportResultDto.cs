namespace Server.Application.Transactions;

/// <summary>The outcome of a CSV transaction import.</summary>
/// <param name="Imported">The number of transactions created.</param>
/// <param name="Skipped">The number of transactions skipped as already present.</param>
/// <param name="Total">The number of CSV data rows read.</param>
/// <param name="Errors">Per-row error messages.</param>
/// <param name="Format">The detected/used CSV format.</param>
/// <param name="Rejected">The number of rows not imported (see <paramref name="Rejections"/>).</param>
/// <param name="Rejections">Why each rejected row was not imported ("Row 7: …").</param>
public record ImportResultDto(int Imported, int Skipped, int Total, List<string> Errors, string Format,
    int Rejected = 0, List<string>? Rejections = null);
