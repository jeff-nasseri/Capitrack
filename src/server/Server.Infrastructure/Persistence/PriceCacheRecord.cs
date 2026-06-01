namespace Server.Infrastructure.Persistence;

/// <summary>Infrastructure-only cache row for a Yahoo quote (5-minute TTL).</summary>
public class PriceCacheRecord
{
    /// <summary>The cached symbol.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>The cached price.</summary>
    public double Price { get; set; }

    /// <summary>The quote currency code.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The instrument's display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>The session percentage change.</summary>
    public double ChangePercent { get; set; }

    /// <summary>When the cache row was last refreshed.</summary>
    public DateTime UpdatedAt { get; set; }
}
