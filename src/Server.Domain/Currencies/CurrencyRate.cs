namespace Server.Domain.Currencies;

/// <summary>A manually-maintained FX rate from one currency to another. Aggregate root.</summary>
public sealed class CurrencyRate : AggregateRoot<int>
{
    public CurrencyCode FromCurrency { get; private set; } = default!;
    public CurrencyCode ToCurrency { get; private set; } = default!;
    public double Rate { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CurrencyRate() { }

    public static CurrencyRate Create(CurrencyCode from, CurrencyCode to, double rate) =>
        new() { FromCurrency = from, ToCurrency = to, Rate = rate };

    public void Update(CurrencyCode from, CurrencyCode to, double rate)
    {
        FromCurrency = from;
        ToCurrency = to;
        Rate = rate;
    }

    public void SetRate(double rate) => Rate = rate;
}
