using Server.Application.Currencies;

namespace Server.Application.Currencies.Queries;

public record GetCurrencyRatesQuery : IRequest<List<CurrencyRateDto>>;

public sealed class GetCurrencyRatesQueryHandler(ICurrencyRateRepository rates)
    : IRequestHandler<GetCurrencyRatesQuery, List<CurrencyRateDto>>
{
    public async Task<List<CurrencyRateDto>> Handle(GetCurrencyRatesQuery request, CancellationToken cancellationToken)
    {
        var all = await rates.ListAsync(cancellationToken);
        return all.Select(r => r.ToDto()).ToList();
    }
}
