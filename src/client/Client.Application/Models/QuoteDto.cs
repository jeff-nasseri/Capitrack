namespace Client.Application.Models;

/// <summary>A live price quote for a symbol, including the day's percentage change and optional staleness/error flags.</summary>
public class QuoteDto
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public string? Currency { get; set; }
    public string? Name { get; set; }
    public double ChangePercent { get; set; }
    public bool? Stale { get; set; }
    public string? Error { get; set; }
}
