namespace Server.Domain.Categories;

/// <summary>
/// A category for goals. Retained for schema parity with the original database
/// (there is no Categories API surface). Entity, not an aggregate root.
/// </summary>
public sealed class Category : Entity<int>
{
    /// <summary>The category's display name.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The optional parent category's identifier.</summary>
    public int? ParentId { get; private set; }

    /// <summary>The category's display color.</summary>
    public Color Color { get; private set; } = Color.Default;

    /// <summary>The icon identifier used by the client.</summary>
    public string Icon { get; private set; } = "folder";

    /// <summary>When the category was created.</summary>
    public DateTime CreatedAt { get; private set; }

    private Category() { }
}
