namespace Client.Application.Models;

/// <summary>A single buy/sell/transfer/dividend transaction within an account.</summary>
public class TransactionDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool IsStaked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AccountName { get; set; }
    public List<TagDto> Tags { get; set; } = [];
    public DateTime? OccurredAt { get; set; }
    public string? ExternalId { get; set; }
}
