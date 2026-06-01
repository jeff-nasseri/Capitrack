namespace Server.Domain.Enums;

/// <summary>
/// The seven kinds of transaction Capitrack understands. Implemented as a
/// value-object smart enum: the <see cref="Value"/> matches the persisted /
/// API string exactly (e.g. "transfer_in") and the behaviour flags drive the
/// holdings and wealth-history maths.
/// </summary>
public sealed class TransactionType : ValueObject
{
    /// <summary>The persisted/API string for this type, e.g. "transfer_in".</summary>
    public string Value { get; }

    private TransactionType(string value) => Value = value;

    /// <summary>A purchase.</summary>
    public static readonly TransactionType Buy = new("buy");

    /// <summary>A sale.</summary>
    public static readonly TransactionType Sell = new("sell");

    /// <summary>An incoming transfer.</summary>
    public static readonly TransactionType TransferIn = new("transfer_in");

    /// <summary>An outgoing transfer.</summary>
    public static readonly TransactionType TransferOut = new("transfer_out");

    /// <summary>A dividend payment.</summary>
    public static readonly TransactionType Dividend = new("dividend");

    /// <summary>An interest payment.</summary>
    public static readonly TransactionType Interest = new("interest");

    /// <summary>A fee.</summary>
    public static readonly TransactionType Fee = new("fee");

    /// <summary>All transaction types in canonical order.</summary>
    public static readonly IReadOnlyList<TransactionType> All =
        new[] { Buy, Sell, TransferIn, TransferOut, Dividend, Interest, Fee };

    /// <summary>Returns true when <paramref name="value"/> matches a known transaction type.</summary>
    public static bool IsValid(string? value) =>
        value is not null && All.Any(t => t.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Parses a transaction type, throwing when the value is unknown.</summary>
    public static TransactionType From(string? value) =>
        All.FirstOrDefault(t => t.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidTransactionTypeException(value ?? "");

    /// <summary>buy / transfer_in — adds shares to a holding.</summary>
    public bool IncreasesQuantity => this == Buy || this == TransferIn;

    /// <summary>sell / transfer_out — removes shares from a holding.</summary>
    public bool DecreasesQuantity => this == Sell || this == TransferOut;

    /// <summary>Types that add to portfolio-history quantity replay (buy, transfer_in, dividend).</summary>
    public bool CountsAsHistoryAdd => this == Buy || this == TransferIn || this == Dividend;

    /// <summary>Types that subtract from portfolio-history quantity replay (sell, transfer_out).</summary>
    public bool CountsAsHistorySub => this == Sell || this == TransferOut;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
