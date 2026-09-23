namespace Client.Application.Models;

/// <summary>A single day's total wealth/cost snapshot used by the calendar, with the raw per-symbol details.</summary>
public class DailyWealthDto
{
    public string Date { get; set; } = "";
    public decimal TotalWealth { get; set; }
    public decimal TotalCost { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public System.Text.Json.JsonElement Details { get; set; }
}
