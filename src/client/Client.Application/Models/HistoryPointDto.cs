namespace Client.Application.Models;

/// <summary>A single OHLCV data point in a symbol's price history.</summary>
public class HistoryPointDto
{
    public DateTime Date { get; set; }
    public double? Close { get; set; }
    public double? Open { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? Volume { get; set; }
}
