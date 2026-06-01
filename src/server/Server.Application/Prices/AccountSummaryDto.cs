namespace Server.Application.Prices;

/// <summary>Per-account roll-up used inside the dashboard summary.</summary>
/// <param name="AccountId">The account's identifier.</param>
/// <param name="AccountName">The account's name.</param>
/// <param name="MarketValue">The current market value of the account's holdings.</param>
/// <param name="CostBasis">The total cost basis of the account's holdings.</param>
/// <param name="HoldingsCount">The number of distinct holdings.</param>
public record AccountSummaryDto(int AccountId, string AccountName, double MarketValue, double CostBasis, int HoldingsCount);
