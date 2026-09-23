namespace Client.Application.Models;

/// <summary>A market-data provider Capitrack can source prices from, and how it is configured.</summary>
public class MarketDataProviderDto
{
    /// <summary>Stable identifier (e.g. "kraken").</summary>
    public string Id { get; set; } = "";

    /// <summary>Human-friendly provider name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Short description of what the provider offers.</summary>
    public string Description { get; set; } = "";

    /// <summary>Whether the provider is used for at least one asset class.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether the provider only works with an API key.</summary>
    public bool RequiresApiKey { get; set; }

    /// <summary>Whether its API-key environment variable is set on the server.</summary>
    public bool ApiKeySet { get; set; }

    /// <summary>The provider's public website.</summary>
    public string Website { get; set; } = "";

    /// <summary>Whether it is the first choice for at least one asset class.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The asset classes it can price: crypto, stock, metal, fx.</summary>
    public List<string> AssetClasses { get; set; } = [];

    /// <summary>The free tier's limits in plain words.</summary>
    public string? Limits { get; set; }

    /// <summary>The server environment variable its API key is read from, if any.</summary>
    public string? ApiKeyEnvVar { get; set; }

    /// <summary>Where to get a free API key, if any.</summary>
    public string? ApiKeyUrl { get; set; }

    /// <summary>Its 1-based position per asset class it is used for.</summary>
    public Dictionary<string, int> Positions { get; set; } = [];

    /// <summary>Whether it can be used now (keyless, or its key is set).</summary>
    public bool Configured { get; set; } = true;
}
