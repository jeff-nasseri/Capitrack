namespace Server.Application.Settings;

/// <summary>A single daily wealth snapshot row inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Date">The snapshot date (yyyy-MM-dd).</param>
/// <param name="Total">The total wealth on that date.</param>
/// <param name="TotalCost">The total cost basis on that date.</param>
/// <param name="BaseCurrency">The base currency the totals are expressed in.</param>
/// <param name="Details">A JSON detail payload for the snapshot.</param>
public record SnapshotDailyWealth(
    string Date,
    double Total,
    double TotalCost,
    string BaseCurrency,
    string Details);
