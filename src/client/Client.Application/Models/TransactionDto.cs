namespace Client.Application.Models;

/// <summary>A single buy/sell/transfer/dividend transaction within an account.</summary>
public class TransactionDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? AccountName { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}
