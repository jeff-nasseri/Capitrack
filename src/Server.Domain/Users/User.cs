namespace Server.Domain.Users;

/// <summary>The (single) application user / admin. Aggregate root.</summary>
public sealed class User : AggregateRoot<int>
{
    public string Username { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public CurrencyCode BaseCurrency { get; private set; } = CurrencyCode.Eur;
    public DateTime CreatedAt { get; private set; }

    private User() { }

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

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;
    public void ChangeBaseCurrency(CurrencyCode currency) => BaseCurrency = currency;
}
