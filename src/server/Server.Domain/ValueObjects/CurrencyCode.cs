namespace Server.Domain.ValueObjects;

/// <summary>An ISO-4217-style currency code (uppercased, non-empty).</summary>
public sealed class CurrencyCode : ValueObject
{
    /// <summary>The uppercased currency code, e.g. "EUR".</summary>
    public string Value { get; }
    private CurrencyCode(string value) => Value = value;

    /// <summary>The Euro currency code.</summary>
    public static readonly CurrencyCode Eur = new("EUR");

    /// <summary>The US Dollar currency code.</summary>
    public static readonly CurrencyCode Usd = new("USD");

    /// <summary>Creates a currency code, throwing when <paramref name="value"/> is empty.</summary>
    public static CurrencyCode Create(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (v.Length == 0) throw new InvalidCurrencyCodeException(value ?? "");
        return new CurrencyCode(v);
    }

    /// <summary>Creates a currency code, returning <paramref name="fallback"/> when the value is blank.</summary>
    public static CurrencyCode CreateOrDefault(string? value, CurrencyCode fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : Create(value);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
