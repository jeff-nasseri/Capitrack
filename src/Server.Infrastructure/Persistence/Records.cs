namespace Server.Infrastructure.Persistence;

/// <summary>Infrastructure-only cache row for a Yahoo quote (5-minute TTL).</summary>
public class PriceCacheRecord
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Name { get; set; } = "";
    public double ChangePercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Infrastructure-only persisted daily wealth snapshot.</summary>
public class DailyWealthRecord
{
    public string Date { get; set; } = "";
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public string Details { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
