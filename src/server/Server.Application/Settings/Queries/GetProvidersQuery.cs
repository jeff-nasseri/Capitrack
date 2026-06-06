namespace Server.Application.Settings.Queries;

/// <summary>Lists the market-data providers Capitrack can use for price lookups.</summary>
public record GetProvidersQuery : IRequest<List<MarketDataProviderDto>>;

/// <summary>Handles <see cref="GetProvidersQuery"/>.</summary>
public sealed class GetProvidersQueryHandler(ILogger<GetProvidersQueryHandler> logger)
    : IRequestHandler<GetProvidersQuery, List<MarketDataProviderDto>>
{
    /// <summary>
    /// Returns the currently-supported providers. Today this is Yahoo Finance only; the shape
    /// is deliberately list-based so future providers (with API keys and enable/disable toggles)
    /// slot in without changing the contract.
    /// </summary>
    public Task<List<MarketDataProviderDto>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetProvidersQuery));

        var providers = new List<MarketDataProviderDto>
        {
            new(
                Id: "yahoo-finance",
                Name: "Yahoo Finance",
                Description: "Real-time and historical market quotes for stocks, ETFs, crypto and commodities, sourced from Yahoo Finance. No API key required.",
                Enabled: true,
                RequiresApiKey: false,
                ApiKeySet: false,
                Website: "https://finance.yahoo.com",
                IsDefault: true)
        };

        return Task.FromResult(providers);
    }
}
