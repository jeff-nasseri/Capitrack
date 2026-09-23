namespace Client.Application.Models;

/// <summary>Mutable model the transaction modal form binds to; read back on save to build the API request.</summary>
public class TransactionFormModel
{
    public int? Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Notes { get; set; } = "";
    public bool IsStaked { get; set; }
    public HashSet<int> TagIds { get; set; } = [];
}
