namespace Server.Domain.Accounts;

/// <summary>The kind of account. Tolerant smart enum: known values are constants, unknown values are accepted (lower-cased) to preserve the open contract.</summary>
public sealed class AccountType : ValueObject
{
    public string Value { get; }
    private AccountType(string value) => Value = value;

    public static readonly AccountType General = new("general");
    public static readonly AccountType Stock = new("stock");
    public static readonly AccountType Crypto = new("crypto");
    public static readonly AccountType Commodity = new("commodity");
    public static readonly AccountType Cash = new("cash");

    private static readonly IReadOnlyList<AccountType> Known = new[] { General, Stock, Crypto, Commodity, Cash };

    public static AccountType From(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return General;
        return Known.FirstOrDefault(t => t.Value.Equals(v, StringComparison.OrdinalIgnoreCase))
               ?? new AccountType(v.ToLowerInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}

/// <summary>Join entity linking an <see cref="Account"/> to a tag.</summary>
public sealed class AccountTag
{
    public int AccountId { get; set; }
    public int TagId { get; set; }
}

/// <summary>An account groups transactions under a base currency. Aggregate root.</summary>
public sealed class Account : AggregateRoot<int>
{
    public string Name { get; private set; } = "";
    public AccountType Type { get; private set; } = AccountType.General;
    public CurrencyCode Currency { get; private set; } = CurrencyCode.Eur;
    public string Description { get; private set; } = "";
    public string Icon { get; private set; } = "wallet";
    public Color Color { get; private set; } = Color.Default;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<AccountTag> _tags = new();
    public IReadOnlyCollection<AccountTag> Tags => _tags.AsReadOnly();

    private Account() { }

    public static Account Create(string? name, AccountType type, CurrencyCode currency,
                                 string? description, string? icon, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account name is required.");
        return new Account
        {
            Name = name.Trim(),
            Type = type,
            Currency = currency,
            Description = description ?? "",
            Icon = string.IsNullOrWhiteSpace(icon) ? "wallet" : icon!,
            Color = color
        };
    }

    public void Update(string? name, AccountType type, CurrencyCode currency,
                       string? description, string? icon, Color color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account name is required.");
        Name = name.Trim();
        Type = type;
        Currency = currency;
        Description = description ?? "";
        Icon = string.IsNullOrWhiteSpace(icon) ? "wallet" : icon!;
        Color = color;
    }
}
