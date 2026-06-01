namespace Server.Application.Currencies;

/// <summary>API representation of a stored currency-conversion rate.</summary>
/// <param name="Id">The rate's identifier.</param>
/// <param name="FromCurrency">The source currency code.</param>
/// <param name="ToCurrency">The target currency code.</param>
/// <param name="Rate">The conversion rate.</param>
/// <param name="UpdatedAt">When the rate was last updated.</param>
public record CurrencyRateDto(int Id, string FromCurrency, string ToCurrency, double Rate, DateTime UpdatedAt);
