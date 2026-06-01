namespace Client.Application.Models;

/// <summary>A point for the line chart (serialized to JS as {date, value}).</summary>
public record ChartPoint(string Date, double Value);
