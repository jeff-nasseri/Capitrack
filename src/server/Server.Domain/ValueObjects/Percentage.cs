namespace Server.Domain.ValueObjects;

/// <summary>A percentage change value (e.g. +12.34 meaning 12.34%).</summary>
public sealed class Percentage : ValueObject
{
    /// <summary>The percentage value.</summary>
    public double Value { get; }

    /// <summary>Creates a percentage from a raw value.</summary>
    public Percentage(double value) => Value = value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => $"{Value.ToString("0.##", CultureInfo.InvariantCulture)}%";
}
