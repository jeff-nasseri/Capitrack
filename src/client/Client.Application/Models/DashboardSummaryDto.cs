namespace Client.Application.Models;

/// <summary>Portfolio-wide totals for the dashboard (wealth, cost, gain) plus a per-account breakdown.</summary>
public class DashboardSummaryDto
{
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public double TotalGain { get; set; }
    public double TotalGainPercent { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public List<AccountSummaryDto> Accounts { get; set; } = [];
    public int HoldingsCount { get; set; }
}
