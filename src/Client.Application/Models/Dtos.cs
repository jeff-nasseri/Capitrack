namespace Client.Application.Models;

// Client-side DTOs. Deserialized from the API's snake_case JSON via the shared
// SnakeCaseLower options configured in ApiClient.

public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; }
}

public class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "general";
    public string Currency { get; set; } = "EUR";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "wallet";
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}

public class TransactionDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? AccountName { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}

public class GoalDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public double TargetAmount { get; set; }
    public string TargetDate { get; set; } = "";
    public string Description { get; set; } = "";
    public int Achieved { get; set; }
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TagDto> Tags { get; set; } = [];
}

public class CurrencyRateDto
{
    public int Id { get; set; }
    public string FromCurrency { get; set; } = "";
    public string ToCurrency { get; set; } = "";
    public double Rate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HoldingDto
{
    public string Symbol { get; set; } = "";
    public double Quantity { get; set; }
    public double? AvgCost { get; set; }
    public double TotalCost { get; set; }
    public int TransactionCount { get; set; }
    public string? FirstTransaction { get; set; }
    public string? LastTransaction { get; set; }
}

// Holding enriched on the client with live price + computed market value/gain.
public class EnrichedHolding : HoldingDto
{
    public int AccountId { get; set; }
    public string AccountCurrency { get; set; } = "USD";
    public double Price { get; set; }
    public string Name { get; set; } = "";
    public double ChangePercent { get; set; }
    public double MarketValue { get; set; }
    public double CostBasis { get; set; }
    public double Gain { get; set; }
    public double GainPct { get; set; }
    public string Currency { get; set; } = "USD";
}

public class QuoteDto
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public string? Currency { get; set; }
    public string? Name { get; set; }
    public double ChangePercent { get; set; }
    public bool? Stale { get; set; }
    public string? Error { get; set; }
}

public class SearchResultDto
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Exchange { get; set; }
}

public class HistoryPointDto
{
    public DateTime Date { get; set; }
    public double? Close { get; set; }
    public double? Open { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? Volume { get; set; }
}

public class AccountSummaryDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public double MarketValue { get; set; }
    public double CostBasis { get; set; }
    public int HoldingsCount { get; set; }
}

public class DashboardSummaryDto
{
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public double TotalGain { get; set; }
    public double TotalGainPercent { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public List<AccountSummaryDto> Accounts { get; set; } = [];
    public int HoldingsCount { get; set; }
}

public class PortfolioHistoryPointDto
{
    public string Date { get; set; } = "";
    public double Value { get; set; }
    public double Cost { get; set; }
    public double Gain { get; set; }
}

public class SessionDto
{
    public string Username { get; set; } = "";
    public string BaseCurrency { get; set; } = "EUR";
}

public class ImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
    public List<string> Errors { get; set; } = [];
    public string Format { get; set; } = "";
    public string? Error { get; set; }
}

public class DetectResultDto
{
    public string Format { get; set; } = "unknown";
    public List<string> Headers { get; set; } = [];
}

public class DailyWealthDto
{
    public string Date { get; set; } = "";
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public System.Text.Json.JsonElement Details { get; set; }
}

public class AboutDto
{
    public string Name { get; set; } = "Capitrack";
    public string Version { get; set; } = "1.0.0";
}

public class DatabaseInfoDto
{
    public string Path { get; set; } = "";
    public bool Exists { get; set; }
}
