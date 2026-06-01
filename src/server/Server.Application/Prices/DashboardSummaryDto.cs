namespace Server.Application.Prices;

/// <summary>The dashboard summary: total wealth, gains and per-account breakdown.</summary>
/// <param name="TotalWealth">The total portfolio market value.</param>
/// <param name="TotalCost">The total cost basis.</param>
/// <param name="TotalGain">The absolute gain (wealth minus cost).</param>
/// <param name="TotalGainPercent">The gain as a percentage of cost.</param>
/// <param name="BaseCurrency">The base currency the totals are expressed in.</param>
/// <param name="Accounts">The per-account roll-ups.</param>
/// <param name="HoldingsCount">The total number of distinct holdings.</param>
public record DashboardSummaryDto(
    double TotalWealth, double TotalCost, double TotalGain, double TotalGainPercent,
    string BaseCurrency, List<AccountSummaryDto> Accounts, int HoldingsCount);
