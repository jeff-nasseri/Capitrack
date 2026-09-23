namespace Server.Application.Settings;

/// <summary>Describes a market-data provider Capitrack can source prices from, and how it is configured.</summary>
/// <param name="Id">A stable identifier (e.g. "coingecko").</param>
/// <param name="Name">The human-friendly provider name.</param>
/// <param name="Description">What the provider offers and its free-tier limits.</param>
/// <param name="Enabled">Whether it is used for at least one asset class.</param>
/// <param name="RequiresApiKey">Whether it only works with an API key.</param>
/// <param name="ApiKeySet">Whether its API-key environment variable is set.</param>
/// <param name="Website">The provider's public website.</param>
/// <param name="IsDefault">Whether it is the first choice for at least one asset class.</param>
/// <param name="AssetClasses">The asset classes it can price (crypto, stock, metal, fx).</param>
/// <param name="Limits">The free tier's limits in plain words.</param>
/// <param name="ApiKeyEnvVar">The environment variable its API key is read from, if any.</param>
/// <param name="ApiKeyUrl">Where to get a free API key, if any.</param>
/// <param name="Positions">Its 1-based position per asset class it is enabled for.</param>
/// <param name="Configured">Whether it can be used now (keyless, or its key is set).</param>
public record MarketDataProviderDto(
    string Id,
    string Name,
    string Description,
    bool Enabled,
    bool RequiresApiKey,
    bool ApiKeySet,
    string Website,
    bool IsDefault,
    List<string>? AssetClasses = null,
    string? Limits = null,
    string? ApiKeyEnvVar = null,
    string? ApiKeyUrl = null,
    Dictionary<string, int>? Positions = null,
    bool Configured = true);
