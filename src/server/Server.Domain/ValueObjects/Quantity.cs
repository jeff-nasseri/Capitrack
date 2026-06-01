namespace Server.Domain.ValueObjects;

/// <summary>A non-negative number of units/shares.</summary>
public sealed class Quantity : ValueObject
{
    /// <summary>The numeric quantity.</summary>
    public double Value { get; }
    private Quantity(double value) => Value = value;

    /// <summary>A quantity of zero.</summary>
    public static readonly Quantity Zero = new(0);

    /// <summary>Creates a quantity, throwing when <paramref name="value"/> is negative.</summary>
    public static Quantity Create(double value)
    {
        if (value < 0) throw new NegativeQuantityException(value);
        return new Quantity(value);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
