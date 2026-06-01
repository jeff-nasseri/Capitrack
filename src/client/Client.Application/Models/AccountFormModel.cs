namespace Client.Application.Models;

/// <summary>Mutable model the account modal form binds to; read back on save to build the API request.</summary>
public class AccountFormModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "general";
    public string Currency { get; set; } = "EUR";
    public string Icon { get; set; } = "wallet";
    public string Color { get; set; } = "#6366f1";
    public string Description { get; set; } = "";
    public HashSet<int> TagIds { get; set; } = [];
}
