namespace Server.Domain.ValueObjects;

/// <summary>A market ticker symbol (uppercased, non-empty), e.g. AAPL, BTC-USD, GC=F.</summary>
public sealed class Symbol : ValueObject
{
    /// <summary>The uppercased ticker symbol.</summary>
    public string Value { get; }
    private Symbol(string value) => Value = value;

    /// <summary>Creates a symbol, throwing when <paramref name="value"/> is empty.</summary>
    public static Symbol Create(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (v.Length == 0) throw new InvalidSymbolException();
        return new Symbol(v);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
