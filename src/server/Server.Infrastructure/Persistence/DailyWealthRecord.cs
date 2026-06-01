namespace Server.Infrastructure.Persistence;

/// <summary>Infrastructure-only persisted daily wealth snapshot.</summary>
public class DailyWealthRecord
{
    /// <summary>The snapshot date (yyyy-MM-dd).</summary>
    public string Date { get; set; } = "";

    /// <summary>The total wealth on that date.</summary>
    public double TotalWealth { get; set; }

    /// <summary>The total cost basis on that date.</summary>
    public double TotalCost { get; set; }

    /// <summary>The base currency the totals are expressed in.</summary>
    public string BaseCurrency { get; set; } = "EUR";

    /// <summary>A JSON detail payload for the snapshot.</summary>
    public string Details { get; set; } = "{}";

    /// <summary>When the snapshot row was last updated.</summary>
    public DateTime UpdatedAt { get; set; }
}
