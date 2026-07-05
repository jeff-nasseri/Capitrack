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

    /// <summary>Whether two-factor (TOTP) authentication is active for this user.</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>The TOTP shared secret (base32). Present once setup has begun; kept even while disabled only during setup.</summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>The shortest session lifetime a user may configure.</summary>
    public const int MinSessionLifetimeMinutes = 15;

    /// <summary>The longest session lifetime a user may configure (also the default).</summary>
    public const int MaxSessionLifetimeMinutes = 120;

    /// <summary>How long a signed-in session stays valid without activity, in minutes (15–120).</summary>
    public int SessionLifetimeMinutes { get; private set; } = MaxSessionLifetimeMinutes;

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

    /// <summary>Stores a freshly-generated TOTP secret to begin 2FA setup. 2FA stays disabled until confirmed.</summary>
    public void BeginTwoFactorSetup(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new DomainException("A two-factor secret is required.");
        TwoFactorSecret = secret;
        TwoFactorEnabled = false;
    }

    /// <summary>Activates 2FA after the user has proven possession of the authenticator (a valid code was verified).</summary>
    public void ConfirmTwoFactor()
    {
        if (string.IsNullOrWhiteSpace(TwoFactorSecret))
            throw new DomainException("Two-factor setup has not been started.");
        TwoFactorEnabled = true;
    }

    /// <summary>Turns 2FA off and discards the secret.</summary>
    public void DisableTwoFactor()
    {
        TwoFactorEnabled = false;
        TwoFactorSecret = null;
    }

    /// <summary>Changes how long sessions stay valid, bounded to 15 minutes – 2 hours.</summary>
    public void ChangeSessionLifetime(int minutes)
    {
        if (minutes is < MinSessionLifetimeMinutes or > MaxSessionLifetimeMinutes)
            throw new DomainException(
                $"Session lifetime must be between {MinSessionLifetimeMinutes} and {MaxSessionLifetimeMinutes} minutes.");
        SessionLifetimeMinutes = minutes;
    }
}
