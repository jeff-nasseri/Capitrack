namespace Client.Application.Models;

/// <summary>A single OHLCV data point in a symbol's price history.</summary>
public class HistoryPointDto
{
    public DateTime Date { get; set; }
    public decimal? Close { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public double? Volume { get; set; }
}
