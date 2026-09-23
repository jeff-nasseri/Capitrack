namespace Server.Application.Settings.Queries;

/// <summary>Lists the market-data providers, their free-tier limits, key state and order per asset class.</summary>
public record GetProvidersQuery : IRequest<List<MarketDataProviderDto>>;

/// <summary>Handles <see cref="GetProvidersQuery"/>.</summary>
public sealed class GetProvidersQueryHandler(IMarketDataService market, ILogger<GetProvidersQueryHandler> logger)
    : IRequestHandler<GetProvidersQuery, List<MarketDataProviderDto>>
{
    /// <summary>Returns every provider with its configuration.</summary>
    public async Task<List<MarketDataProviderDto>> Handle(GetProvidersQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetProvidersQuery));
        return await market.ProvidersAsync(cancellationToken);
    }
}
