namespace Client.Application.Models;

/// <summary>A transaction the user chose to import from the "Check &amp; Import" preview (sent to /import/selected).</summary>
public class SelectedTransactionDto
{
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsStaked { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? ExternalId { get; set; }
}
