namespace Server.Application.Prices;

/// <summary>A lightweight daily wealth snapshot returned when saving today's value.</summary>
/// <param name="Date">The snapshot date (yyyy-MM-dd).</param>
/// <param name="TotalWealth">The total wealth on that date.</param>
/// <param name="TotalCost">The total cost basis on that date.</param>
/// <param name="BaseCurrency">The base currency the totals are expressed in.</param>
public record DailyWealthSnapshotDto(string Date, double TotalWealth, double TotalCost, string BaseCurrency);
