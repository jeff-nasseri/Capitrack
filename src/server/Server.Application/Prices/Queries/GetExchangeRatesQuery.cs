namespace Server.Application.Prices.Queries;

/// <summary>Returns today's rates from the given currencies to the base currency, as the dashboard uses them.</summary>
/// <param name="Currencies">Currency codes (e.g. USD, GBP).</param>
public record GetExchangeRatesQuery(IReadOnlyList<string> Currencies) : IRequest<ExchangeRatesDto>;

/// <summary>Handles <see cref="GetExchangeRatesQuery"/>.</summary>
public sealed class GetExchangeRatesQueryHandler(IWealthService wealth, ILogger<GetExchangeRatesQueryHandler> logger)
    : IRequestHandler<GetExchangeRatesQuery, ExchangeRatesDto>
{
    public async Task<ExchangeRatesDto> Handle(GetExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetExchangeRatesQuery));
        return await wealth.RatesToBaseAsync(request.Currencies);
    }
}
