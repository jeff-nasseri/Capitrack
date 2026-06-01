using Server.Domain.Currencies;

namespace Server.Application.Currencies;

/// <summary>AutoMapper profile mapping <see cref="CurrencyRate"/> aggregates to <see cref="CurrencyRateDto"/>.</summary>
public sealed class CurrencyMappingProfile : Profile
{
    /// <summary>Configures the currency-rate mappings.</summary>
    public CurrencyMappingProfile()
    {
        CreateMap<CurrencyRate, CurrencyRateDto>()
            .ForCtorParam("FromCurrency", o => o.MapFrom(s => s.FromCurrency.Value))
            .ForCtorParam("ToCurrency", o => o.MapFrom(s => s.ToCurrency.Value));
    }
}
