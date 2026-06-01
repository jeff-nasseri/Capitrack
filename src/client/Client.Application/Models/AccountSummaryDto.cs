namespace Client.Application.Models;

/// <summary>Per-account totals (market value, cost basis, holdings count) within a dashboard summary.</summary>
public class AccountSummaryDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public double MarketValue { get; set; }
    public double CostBasis { get; set; }
    public int HoldingsCount { get; set; }
}
