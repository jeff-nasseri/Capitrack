namespace Client.Application.Models;

/// <summary>A label that can be attached to accounts, transactions and goals. Deserialized from the API's snake_case JSON.</summary>
public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; }
}
