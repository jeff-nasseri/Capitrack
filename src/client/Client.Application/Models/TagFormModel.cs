namespace Client.Application.Models;

/// <summary>Mutable model the tag modal form binds to; read back on save to build the API request.</summary>
public class TagFormModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
}
