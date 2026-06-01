namespace Server.Domain.ValueObjects;

/// <summary>An amount of money in a specific currency. Arithmetic is guarded to a single currency.</summary>
public sealed class Money : ValueObject
{
    /// <summary>The monetary amount.</summary>
    public double Amount { get; }

    /// <summary>The currency the amount is denominated in.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Creates a money value from an amount and currency.</summary>
    public Money(double amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>A zero amount in the given currency.</summary>
    public static Money Zero(CurrencyCode currency) => new(0, currency);

    /// <summary>Adds another amount, requiring the same currency.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts another amount, requiring the same currency.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Scales the amount by a factor, preserving the currency.</summary>
    public Money Multiply(double factor) => new(Amount * factor, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
            throw new CurrencyMismatchException(Currency.Value, other.Currency.Value);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Math.Round(Amount, 8);
        yield return Currency;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount.ToString("0.####", CultureInfo.InvariantCulture)} {Currency}";
}
