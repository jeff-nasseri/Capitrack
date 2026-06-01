namespace Server.Domain.Tags;

/// <summary>A label that can be attached to accounts, transactions and goals. Aggregate root.</summary>
public sealed class Tag : AggregateRoot<int>
{
    /// <summary>The tag's display name.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The tag's display color.</summary>
    public Color Color { get; private set; } = Color.Default;

    /// <summary>When the tag was created.</summary>
    public DateTime CreatedAt { get; private set; }

    private Tag() { }

    /// <summary>Creates a new tag, requiring a non-empty name.</summary>
    public static Tag Create(string? name, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tag name is required.");
        return new Tag { Name = name.Trim(), Color = color };
    }

    /// <summary>Updates the tag's name and color, requiring a non-empty name.</summary>
    public void Update(string? name, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tag name is required.");
        Name = name.Trim();
        Color = color;
    }
}
