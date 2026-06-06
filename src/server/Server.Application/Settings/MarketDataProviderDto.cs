namespace Server.Application.Settings;

/// <summary>Describes a market-data provider Capitrack can source live prices from.</summary>
/// <param name="Id">A stable identifier (e.g. "yahoo-finance").</param>
/// <param name="Name">The human-friendly provider name.</param>
/// <param name="Description">A short description of what the provider offers.</param>
/// <param name="Enabled">Whether the provider is currently active.</param>
/// <param name="RequiresApiKey">Whether the provider needs an API key.</param>
/// <param name="ApiKeySet">Whether an API key has already been configured.</param>
/// <param name="Website">The provider's public website.</param>
/// <param name="IsDefault">Whether this is the default provider used for price lookups.</param>
public record MarketDataProviderDto(
    string Id,
    string Name,
    string Description,
    bool Enabled,
    bool RequiresApiKey,
    bool ApiKeySet,
    string Website,
    bool IsDefault);
