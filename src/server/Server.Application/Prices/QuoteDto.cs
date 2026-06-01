using System.Text.Json.Serialization;

namespace Server.Application.Prices;

/// <summary>A market quote for a single symbol, optionally flagged as stale.</summary>
public class QuoteDto
{
    /// <summary>The quoted symbol.</summary>
    public string Symbol { get; set; } = "";

    /// <summary>The latest price.</summary>
    public double Price { get; set; }

    /// <summary>The quote currency code.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The instrument's display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>The percentage change for the session.</summary>
    public double ChangePercent { get; set; }

    /// <summary>True when the quote was served from a stale cache; omitted otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stale { get; set; }

    /// <summary>An error message when the quote could not be retrieved; omitted otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}
