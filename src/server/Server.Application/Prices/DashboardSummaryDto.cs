namespace Server.Application.Prices;

/// <summary>The dashboard summary: total wealth, gains and per-account breakdown.</summary>
/// <param name="TotalWealth">The total portfolio market value.</param>
/// <param name="TotalCost">The total cost basis.</param>
/// <param name="TotalGain">The absolute gain (wealth minus cost).</param>
/// <param name="TotalGainPercent">The gain as a percentage of cost.</param>
/// <param name="BaseCurrency">The base currency the totals are expressed in.</param>
/// <param name="Accounts">The per-account roll-ups.</param>
/// <param name="HoldingsCount">The total number of distinct holdings.</param>
/// <param name="TodayChange">The change in value over the current session, in the base currency.</param>
public record DashboardSummaryDto(
    decimal TotalWealth, decimal TotalCost, decimal TotalGain, decimal TotalGainPercent,
    string BaseCurrency, List<AccountSummaryDto> Accounts, int HoldingsCount, decimal TodayChange = 0);
