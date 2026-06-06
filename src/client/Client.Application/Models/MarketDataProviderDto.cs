namespace Client.Application.Models;

/// <summary>A market-data provider Capitrack can source live prices from (e.g. Yahoo Finance).</summary>
public class MarketDataProviderDto
{
    /// <summary>Stable identifier (e.g. "yahoo-finance").</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-friendly provider name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Short description of what the provider offers.</summary>
    public string Description { get; set; } = "";

    /// <summary>Whether the provider is currently active.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether the provider needs an API key to be configured.</summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>Whether an API key has already been supplied for this provider.</summary>
    public bool ApiKeySet { get; set; }

    /// <summary>The provider's public website.</summary>
    public string Website { get; set; } = "";

    /// <summary>Whether this is the default provider used for price lookups.</summary>
    public bool IsDefault { get; set; }
}
