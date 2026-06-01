namespace Server.Domain.ValueObjects;

/// <summary>A hex color string such as #6366f1.</summary>
public sealed class Color : ValueObject
{
    private static readonly Regex Hex = new("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$", RegexOptions.Compiled);

    /// <summary>The hex color string (including the leading '#').</summary>
    public string Value { get; }
    private Color(string value) => Value = value;

    /// <summary>The default brand color.</summary>
    public static readonly Color Default = new("#6366f1");

    /// <summary>Creates a color, throwing when <paramref name="value"/> is not a valid hex color.</summary>
    public static Color Create(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (!Hex.IsMatch(v)) throw new InvalidColorException(value ?? "");
        return new Color(v);
    }

    /// <summary>Creates a color, returning <see cref="Default"/> when the value is blank.</summary>
    public static Color CreateOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Default : Create(value);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value.ToLowerInvariant(); }

    /// <inheritdoc />
    public override string ToString() => Value;
}
