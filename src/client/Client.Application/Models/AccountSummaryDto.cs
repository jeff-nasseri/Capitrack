namespace Client.Application.Models;

/// <summary>Per-account totals (market value, cost basis, holdings count) within a dashboard summary.</summary>
public class AccountSummaryDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public decimal MarketValue { get; set; }
    public decimal CostBasis { get; set; }
    public int HoldingsCount { get; set; }
}
