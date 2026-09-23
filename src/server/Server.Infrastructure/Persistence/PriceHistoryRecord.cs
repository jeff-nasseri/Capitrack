namespace Server.Infrastructure.Persistence;

/// <summary>One cached daily close from one provider. Past closes never change, so they are kept for good.</summary>
public class PriceHistoryRecord
{
    /// <summary>The Capitrack symbol.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>The provider id the close came from.</summary>
    public string Provider { get; set; } = "";

    /// <summary>The day (yyyy-MM-dd).</summary>
    public string Date { get; set; } = "";

    /// <summary>The closing price.</summary>
    public decimal Close { get; set; }

    /// <summary>The currency of the close.</summary>
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// The date range a provider has already been asked for a symbol, so days it had no close for
/// (weekends, before its history starts) are not requested again.
/// </summary>
public class PriceCoverageRecord
{
    /// <summary>The Capitrack symbol.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>The provider id.</summary>
    public string Provider { get; set; } = "";

    /// <summary>The first day covered (yyyy-MM-dd).</summary>
    public string From { get; set; } = "";

    /// <summary>The last day covered (yyyy-MM-dd); never today, whose close is not final.</summary>
    public string To { get; set; } = "";

    /// <summary>When the range was last fetched.</summary>
    public DateTime FetchedAt { get; set; }
}

/// <summary>A persisted application setting (key/value).</summary>
public class AppSettingRecord
{
    /// <summary>The setting's key.</summary>
    public string Key { get; set; } = "";

    /// <summary>The setting's value (JSON for structured settings).</summary>
    public string Value { get; set; } = "";
}
