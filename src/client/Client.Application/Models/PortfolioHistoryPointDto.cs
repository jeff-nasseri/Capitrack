namespace Client.Application.Models;

/// <summary>A single point in the portfolio's value/cost/gain history time series.</summary>
public class PortfolioHistoryPointDto
{
    public string Date { get; set; } = "";
    public double Value { get; set; }
    public double Cost { get; set; }
    public double Gain { get; set; }
}
