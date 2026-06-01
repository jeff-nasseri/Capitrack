namespace Server.Domain.Accounts;

/// <summary>The kind of account. Tolerant smart enum: known values are constants, unknown values are accepted (lower-cased) to preserve the open contract.</summary>
public sealed class AccountType : ValueObject
{
    /// <summary>The lower-cased account-type string, e.g. "stock".</summary>
    public string Value { get; }
    private AccountType(string value) => Value = value;

    /// <summary>A general-purpose account.</summary>
    public static readonly AccountType General = new("general");

    /// <summary>An equities/stock account.</summary>
    public static readonly AccountType Stock = new("stock");

    /// <summary>A cryptocurrency account.</summary>
    public static readonly AccountType Crypto = new("crypto");

    /// <summary>A commodities account.</summary>
    public static readonly AccountType Commodity = new("commodity");

    /// <summary>A cash account.</summary>
    public static readonly AccountType Cash = new("cash");

    private static readonly IReadOnlyList<AccountType> Known = new[] { General, Stock, Crypto, Commodity, Cash };

    /// <summary>Parses an account type, defaulting to <see cref="General"/> and accepting unknown values verbatim (lower-cased).</summary>
    public static AccountType From(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) return General;
        return Known.FirstOrDefault(t => t.Value.Equals(v, StringComparison.OrdinalIgnoreCase))
               ?? new AccountType(v.ToLowerInvariant());
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
