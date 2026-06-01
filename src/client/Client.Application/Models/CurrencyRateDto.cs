namespace Client.Application.Models;

/// <summary>A user-defined conversion rate between two currencies.</summary>
public class CurrencyRateDto
{
    public int Id { get; set; }
    public string FromCurrency { get; set; } = "";
    public string ToCurrency { get; set; } = "";
    public double Rate { get; set; }
    public DateTime UpdatedAt { get; set; }
}
