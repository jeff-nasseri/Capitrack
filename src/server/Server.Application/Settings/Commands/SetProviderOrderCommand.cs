using ValidationException = Server.Application.Common.Exceptions.ValidationException;

namespace Server.Application.Settings.Commands;

/// <summary>Sets which providers are used, in what order, per asset class (e.g. {"crypto": ["kraken", "coingecko"]}).</summary>
/// <param name="Order">Provider ids in priority order, keyed by asset class (crypto, stock, metal, fx). Omitted classes keep their order.</param>
public record SetProviderOrderCommand(Dictionary<string, List<string>>? Order) : IRequest<List<MarketDataProviderDto>>;

/// <summary>Handles <see cref="SetProviderOrderCommand"/>.</summary>
public sealed class SetProviderOrderHandler(IMarketDataService market, ILogger<SetProviderOrderHandler> logger)
    : IRequestHandler<SetProviderOrderCommand, List<MarketDataProviderDto>>
{
    /// <summary>Validates and saves the order, then returns the updated provider list.</summary>
    public async Task<List<MarketDataProviderDto>> Handle(SetProviderOrderCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(SetProviderOrderCommand));
        var current = await market.ProvidersAsync(cancellationToken);
        var order = new Dictionary<AssetClass, List<string>>();
        foreach (var assetClass in Enum.GetValues<AssetClass>())
        {
            var key = assetClass.ToString().ToLowerInvariant();
            order[assetClass] = request.Order is not null && request.Order.TryGetValue(key, out var ids)
                ? ids
                : current.Where(p => p.Positions?.ContainsKey(key) == true).OrderBy(p => p.Positions![key]).Select(p => p.Id).ToList();
        }
        try { await market.SetProviderOrderAsync(order, cancellationToken); }
        catch (ArgumentException e) { throw new ValidationException(e.Message); }
        return await market.ProvidersAsync(cancellationToken);
    }
}
