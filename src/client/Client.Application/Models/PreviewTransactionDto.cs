namespace Client.Application.Models;

/// <summary>A single parsed-but-not-yet-imported transaction row shown in the "Check &amp; Import" preview.</summary>
public class PreviewTransactionDto
{
    public int Index { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsDuplicate { get; set; }
    public bool CanStake { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? ExternalId { get; set; }
}
