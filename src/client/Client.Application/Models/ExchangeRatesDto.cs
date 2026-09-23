namespace Client.Application.Models;

/// <summary>Today's rates to the base currency, exactly as the server converts the dashboard totals.</summary>
public class ExchangeRatesDto
{
    /// <summary>The base currency.</summary>
    public string BaseCurrency { get; set; } = "EUR";

    /// <summary>Units of the base currency per 1 unit of each currency; currencies with no known rate are absent.</summary>
    public Dictionary<string, decimal> Rates { get; set; } = [];

    /// <summary>The amount in the base currency, or unchanged when its currency has no known rate.</summary>
    public decimal ToBase(decimal amount, string? currency) =>
        currency is not null && Rates.TryGetValue(currency, out var rate) ? amount * rate : amount;
}
