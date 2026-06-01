namespace Server.Domain.Currencies;

/// <summary>A manually-maintained FX rate from one currency to another. Aggregate root.</summary>
public sealed class CurrencyRate : AggregateRoot<int>
{
    /// <summary>The source currency.</summary>
    public CurrencyCode FromCurrency { get; private set; } = default!;

    /// <summary>The target currency.</summary>
    public CurrencyCode ToCurrency { get; private set; } = default!;

    /// <summary>The conversion rate from source to target.</summary>
    public double Rate { get; private set; }

    /// <summary>When the rate was last updated.</summary>
    public DateTime UpdatedAt { get; private set; }

    private CurrencyRate() { }

    /// <summary>Creates a new currency rate.</summary>
    public static CurrencyRate Create(CurrencyCode from, CurrencyCode to, double rate) =>
        new() { FromCurrency = from, ToCurrency = to, Rate = rate };

    /// <summary>Updates the currency pair and rate.</summary>
    public void Update(CurrencyCode from, CurrencyCode to, double rate)
    {
        FromCurrency = from;
        ToCurrency = to;
        Rate = rate;
    }

    /// <summary>Updates only the rate value.</summary>
    public void SetRate(double rate) => Rate = rate;
}
