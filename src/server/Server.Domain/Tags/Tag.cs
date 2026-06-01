namespace Server.Domain.Tags;

/// <summary>A label that can be attached to accounts, transactions and goals. Aggregate root.</summary>
public sealed class Tag : AggregateRoot<int>
{
    public string Name { get; private set; } = "";
    public Color Color { get; private set; } = Color.Default;
    public DateTime CreatedAt { get; private set; }

    private Tag() { }

    public static Tag Create(string? name, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tag name is required.");
        return new Tag { Name = name.Trim(), Color = color };
    }

    public void Update(string? name, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tag name is required.");
        Name = name.Trim();
        Color = color;
    }
}
