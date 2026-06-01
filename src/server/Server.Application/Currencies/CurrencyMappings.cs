using Server.Domain.Currencies;

namespace Server.Application.Currencies;

public static class CurrencyMappings
{
    public static CurrencyRateDto ToDto(this CurrencyRate r) => new(
        r.Id, r.FromCurrency.Value, r.ToCurrency.Value, r.Rate, r.UpdatedAt);
}
