namespace Client.Application.Models;

/// <summary>A single point in the portfolio's value/cost/gain history time series.</summary>
public class PortfolioHistoryPointDto
{
    public string Date { get; set; } = "";
    public decimal Value { get; set; }
    public decimal Cost { get; set; }
    public decimal Gain { get; set; }
}
