namespace Server.Domain.Categories;

/// <summary>
/// A category for goals. Retained for schema parity with the original database
/// (there is no Categories API surface). Entity, not an aggregate root.
/// </summary>
public sealed class Category : Entity<int>
{
    public string Name { get; private set; } = "";
    public int? ParentId { get; private set; }
    public Color Color { get; private set; } = Color.Default;
    public string Icon { get; private set; } = "folder";
    public DateTime CreatedAt { get; private set; }

    private Category() { }
}
