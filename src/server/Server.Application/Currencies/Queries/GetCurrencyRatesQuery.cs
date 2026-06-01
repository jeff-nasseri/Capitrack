namespace Server.Application.Currencies.Queries;

/// <summary>Returns all stored currency-conversion rates.</summary>
public record GetCurrencyRatesQuery : IRequest<List<CurrencyRateDto>>;

/// <summary>Handles <see cref="GetCurrencyRatesQuery"/>.</summary>
public sealed class GetCurrencyRatesQueryHandler(
    ICurrencyRateRepository rates,
    IMapper mapper,
    ILogger<GetCurrencyRatesQueryHandler> logger)
    : IRequestHandler<GetCurrencyRatesQuery, List<CurrencyRateDto>>
{
    /// <summary>Loads all rates and returns the resulting DTOs.</summary>
    public async Task<List<CurrencyRateDto>> Handle(GetCurrencyRatesQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetCurrencyRatesQuery));

        var all = await rates.ListAsync(cancellationToken);
        return all.Select(r => mapper.Map<CurrencyRateDto>(r)).ToList();
    }
}
