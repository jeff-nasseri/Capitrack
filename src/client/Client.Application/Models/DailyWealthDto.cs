namespace Client.Application.Models;

/// <summary>A single day's total wealth/cost snapshot used by the calendar, with the raw per-symbol details.</summary>
public class DailyWealthDto
{
    public string Date { get; set; } = "";
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public System.Text.Json.JsonElement Details { get; set; }
}
