namespace Client.Application.Models;

/// <summary>A <see cref="HoldingDto"/> enriched on the client with the live price plus computed market value, cost basis and gain.</summary>
public class EnrichedHolding : HoldingDto
{
    public int AccountId { get; set; }
    public string AccountCurrency { get; set; } = "USD";
    public decimal Price { get; set; }
    public string Name { get; set; } = "";
    public double ChangePercent { get; set; }
    public decimal MarketValue { get; set; }
    public decimal CostBasis { get; set; }
    public decimal Gain { get; set; }
    public double GainPct { get; set; }
    public string Currency { get; set; } = "USD";
}
