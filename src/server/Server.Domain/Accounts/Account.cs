namespace Server.Domain.Accounts;

/// <summary>An account groups transactions under a base currency. Aggregate root.</summary>
public sealed class Account : AggregateRoot<int>
{
    /// <summary>The account's display name.</summary>
    public string Name { get; private set; } = "";

    /// <summary>The account type.</summary>
    public AccountType Type { get; private set; } = AccountType.General;

    /// <summary>The account's base currency.</summary>
    public CurrencyCode Currency { get; private set; } = CurrencyCode.Eur;

    /// <summary>A free-text description.</summary>
    public string Description { get; private set; } = "";

    /// <summary>The icon identifier used by the client.</summary>
    public string Icon { get; private set; } = "wallet";

    /// <summary>The account's display color.</summary>
    public Color Color { get; private set; } = Color.Default;

    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>When the account was last updated.</summary>
    public DateTime UpdatedAt { get; private set; }

    private readonly List<AccountTag> _tags = new();

    /// <summary>The tags attached to this account.</summary>
    public IReadOnlyCollection<AccountTag> Tags => _tags.AsReadOnly();

    private Account() { }

    /// <summary>Creates a new account, requiring a non-empty name.</summary>
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

    /// <summary>Updates the account's editable fields, requiring a non-empty name.</summary>
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
