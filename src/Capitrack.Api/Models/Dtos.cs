using System.Text.Json.Serialization;

namespace Capitrack.Api.Models;

// ---- Response DTOs (serialized via the global snake_case policy) ----

public record TagDto(int Id, string Name, string Color, DateTime CreatedAt);

public record AccountDto(
    int Id, string Name, string Type, string Currency, string Description,
    string Icon, string Color, DateTime CreatedAt, DateTime UpdatedAt, List<TagDto> Tags);

public record TransactionDto(
    int Id, int AccountId, string Symbol, string Type, double Quantity, double Price,
    double Fee, string Currency, string Date, string Notes, DateTime CreatedAt,
    string? AccountName, List<TagDto> Tags);

public record GoalDto(
    int Id, string Title, double TargetAmount, string TargetDate, string Description,
    int Achieved, int? CategoryId, DateTime CreatedAt, DateTime UpdatedAt, List<TagDto> Tags);

public record CurrencyRateDto(int Id, string FromCurrency, string ToCurrency, double Rate, DateTime UpdatedAt);

public record HoldingDto(
    string Symbol, double Quantity, double? AvgCost, double TotalCost,
    int TransactionCount, string? FirstTransaction, string? LastTransaction);

public record SessionDto(string Username, string BaseCurrency);

public class QuoteDto
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Name { get; set; } = "";
    public double ChangePercent { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Stale { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

public record SearchResultDto(string Symbol, string Name, string? Type, string? Exchange);

public record HistoryPointDto(DateTime Date, double? Close, double? Open, double? High, double? Low, double? Volume);

public record AccountSummaryDto(int AccountId, string AccountName, double MarketValue, double CostBasis, int HoldingsCount);

public record DashboardSummaryDto(
    double TotalWealth, double TotalCost, double TotalGain, double TotalGainPercent,
    string BaseCurrency, List<AccountSummaryDto> Accounts, int HoldingsCount);

public record PortfolioHistoryPointDto(string Date, double Value, double Cost, double Gain);

public record ImportResultDto(int Imported, int Skipped, int Total, List<string> Errors, string Format);

// ---- Request DTOs ----
// Clean PascalCase property names map to snake_case JSON via the global policy
// (AccountId -> account_id, TagIds -> tag_ids, BaseCurrency -> base_currency...).
// The password endpoint is the one camelCase exception in the original API.

public record LoginRequest(string? Username, string? Password);

public class PasswordRequest
{
    [JsonPropertyName("currentPassword")] public string? CurrentPassword { get; set; }
    [JsonPropertyName("newPassword")] public string? NewPassword { get; set; }
}

public record CurrencyRequest(string? BaseCurrency);

public record AccountRequest(
    string? Name, string? Type, string? Currency, string? Description,
    string? Icon, string? Color, List<int>? TagIds);

public record TransactionRequest(
    int? AccountId, string? Symbol, string? Type, double? Quantity, double? Price,
    double? Fee, string? Currency, string? Date, string? Notes, List<int>? TagIds);

public record GoalRequest(
    string? Title, double? TargetAmount, string? TargetDate, string? Description,
    bool? Achieved, int? CategoryId, List<int>? TagIds);

public record TagRequest(string? Name, string? Color);
public record RateRequest(string? FromCurrency, string? ToCurrency, double? Rate);
public record QuotesRequest(List<string>? Symbols);
public record DatabasePathRequest(string? Path);
