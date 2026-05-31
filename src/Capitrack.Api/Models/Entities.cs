namespace Capitrack.Api.Models;

// Database entities. Column/table naming uses EF Core defaults (a fresh SQLite
// database is created via EnsureCreated). The public JSON contract is produced
// by the global snake_case naming policy + the DTOs in Dtos.cs.

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string BaseCurrency { get; set; } = "EUR";
    public DateTime CreatedAt { get; set; }
}

public class Account
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
}

public class Transaction
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "buy"; // buy|sell|transfer_in|transfer_out|dividend|interest|fee
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Fee { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Date { get; set; } = ""; // YYYY-MM-DD (text, like the original)
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public string Color { get; set; } = "#6366f1";
    public string Icon { get; set; } = "folder";
    public DateTime CreatedAt { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";
    public DateTime CreatedAt { get; set; }
}

public class Goal
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public double TargetAmount { get; set; }
    public string TargetDate { get; set; } = ""; // YYYY-MM-DD (text)
    public string Description { get; set; } = "";
    public int Achieved { get; set; } // 0/1, like the original
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CurrencyRate
{
    public int Id { get; set; }
    public string FromCurrency { get; set; } = "";
    public string ToCurrency { get; set; } = "";
    public double Rate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PriceCache
{
    public string Symbol { get; set; } = "";
    public double Price { get; set; }
    public string Currency { get; set; } = "USD";
    public string Name { get; set; } = "";
    public double ChangePercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DailyWealth
{
    public string Date { get; set; } = ""; // YYYY-MM-DD (PK)
    public double TotalWealth { get; set; }
    public double TotalCost { get; set; }
    public string BaseCurrency { get; set; } = "EUR";
    public string Details { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

// Join entities (many-to-many), mirroring account_tags / goal_tags / transaction_tags.
public class AccountTag
{
    public int AccountId { get; set; }
    public int TagId { get; set; }
}

public class GoalTag
{
    public int GoalId { get; set; }
    public int TagId { get; set; }
}

public class TransactionTag
{
    public int TransactionId { get; set; }
    public int TagId { get; set; }
}
