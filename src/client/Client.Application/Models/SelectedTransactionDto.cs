namespace Client.Application.Models;

/// <summary>A transaction the user chose to import from the "Check &amp; Import" preview (sent to /import/selected).</summary>
public class SelectedTransactionDto
{
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsStaked { get; set; }
}
