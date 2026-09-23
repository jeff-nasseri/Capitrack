namespace Client.Application.Models;

/// <summary>Portfolio-wide totals for the dashboard (wealth, cost, gain) plus a per-account breakdown.</summary>
public class DashboardSummaryDto
{
    public decimal TotalWealth { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalGain { get; set; }
    public double TotalGainPercent { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public List<AccountSummaryDto> Accounts { get; set; } = [];
    public int HoldingsCount { get; set; }
    public decimal TodayChange { get; set; }
}
