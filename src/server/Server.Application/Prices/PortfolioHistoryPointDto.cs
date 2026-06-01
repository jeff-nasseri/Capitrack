namespace Server.Application.Prices;

/// <summary>A single point in the portfolio value history series.</summary>
/// <param name="Date">The point's date (yyyy-MM-dd).</param>
/// <param name="Value">The portfolio market value on that date.</param>
/// <param name="Cost">The cost basis on that date.</param>
/// <param name="Gain">The gain (value minus cost) on that date.</param>
public record PortfolioHistoryPointDto(string Date, double Value, double Cost, double Gain);
