namespace Server.Application.Prices;

/// <summary>Today's rates to the base currency: the manual rate from settings when set, otherwise the latest ECB reference rate.</summary>
/// <param name="BaseCurrency">The base currency.</param>
/// <param name="Rates">Units of the base currency per 1 unit of each currency (the base itself is 1). Currencies with no known rate are left out.</param>
public record ExchangeRatesDto(string BaseCurrency, Dictionary<string, decimal> Rates);
