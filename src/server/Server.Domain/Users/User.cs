namespace Server.Domain.Users;

/// <summary>The (single) application user / admin. Aggregate root.</summary>
public sealed class User : AggregateRoot<int>
{
    /// <summary>The user's login name.</summary>
    public string Username { get; private set; } = "";

    /// <summary>The BCrypt password hash.</summary>
    public string PasswordHash { get; private set; } = "";

    /// <summary>The user's preferred base currency.</summary>
    public CurrencyCode BaseCurrency { get; private set; } = CurrencyCode.Eur;

    /// <summary>When the user was created.</summary>
    public DateTime CreatedAt { get; private set; }

    private User() { }

    /// <summary>Creates a new user, requiring a non-empty username.</summary>
    public static User Create(string? username, string passwordHash, CurrencyCode baseCurrency)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new DomainException("Username is required.");
        return new User
        {
            Username = username.Trim(),
            PasswordHash = passwordHash,
            BaseCurrency = baseCurrency
        };
    }

    /// <summary>Replaces the stored password hash.</summary>
    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>Changes the user's base currency.</summary>
    public void ChangeBaseCurrency(CurrencyCode currency) => BaseCurrency = currency;
}
