namespace Client.Application.Models;

/// <summary>A portfolio account (e.g. a brokerage or wallet) with its display metadata and tags.</summary>
public class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "general";
    public string Currency { get; set; } = "EUR";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "wallet";
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}
