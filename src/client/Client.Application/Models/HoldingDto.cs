namespace Client.Application.Models;

/// <summary>An aggregated position in a single symbol (quantity and cost basis) as returned by the API.</summary>
public class HoldingDto
{
    public string Symbol { get; set; } = "";
    public double Quantity { get; set; }
    public double? AvgCost { get; set; }
    public double TotalCost { get; set; }
    public int TransactionCount { get; set; }
    public string? FirstTransaction { get; set; }
    public string? LastTransaction { get; set; }
}
