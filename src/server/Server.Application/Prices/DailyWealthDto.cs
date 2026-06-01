namespace Server.Application.Prices;

/// <summary>A stored daily wealth snapshot with optional detail payload.</summary>
/// <param name="Date">The snapshot date (yyyy-MM-dd).</param>
/// <param name="TotalWealth">The total wealth on that date.</param>
/// <param name="TotalCost">The total cost basis on that date.</param>
/// <param name="BaseCurrency">The base currency the totals are expressed in.</param>
/// <param name="Details">An optional per-snapshot detail payload.</param>
public record DailyWealthDto(string Date, double TotalWealth, double TotalCost, string BaseCurrency, object? Details);
