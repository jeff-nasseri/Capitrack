namespace Server.Domain.ValueObjects;

/// <summary>An amount of money in a specific currency. Arithmetic is guarded to a single currency.</summary>
public sealed class Money : ValueObject
{
    public double Amount { get; }
    public CurrencyCode Currency { get; }

    public Money(double amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(CurrencyCode currency) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(double factor) => new(Amount * factor, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
            throw new CurrencyMismatchException(Currency.Value, other.Currency.Value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Math.Round(Amount, 8);
        yield return Currency;
    }

    public override string ToString() => $"{Amount.ToString("0.####", CultureInfo.InvariantCulture)} {Currency}";
}
