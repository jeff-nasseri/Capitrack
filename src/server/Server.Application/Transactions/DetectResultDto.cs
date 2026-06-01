namespace Server.Application.Transactions;

/// <summary>The result of detecting a CSV file's format.</summary>
/// <param name="Format">The detected format identifier.</param>
/// <param name="Headers">The CSV header columns.</param>
public record DetectResultDto(string Format, List<string> Headers);
