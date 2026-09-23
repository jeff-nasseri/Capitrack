namespace Server.Domain.Transactions;

/// <summary>A single trade/movement on an account. Aggregate root.</summary>
public sealed class Transaction : AggregateRoot<int>
{
    /// <summary>The owning account's identifier.</summary>
    public int AccountId { get; private set; }

    /// <summary>The traded symbol.</summary>
    public Symbol Symbol { get; private set; } = default!;

    /// <summary>The kind of transaction.</summary>
    public TransactionType Type { get; private set; } = TransactionType.Buy;

    /// <summary>The traded quantity.</summary>
    public Quantity Quantity { get; private set; } = Quantity.Zero;

    /// <summary>The unit price.</summary>
    public decimal Price { get; private set; }

    /// <summary>Any transaction fee.</summary>
    public decimal Fee { get; private set; }

    /// <summary>The transaction currency.</summary>
    public CurrencyCode Currency { get; private set; } = CurrencyCode.Eur;

    /// <summary>The trade date.</summary>
    public TradeDate Date { get; private set; } = default!;

    /// <summary>Free-text notes.</summary>
    public string Notes { get; private set; } = "";

    /// <summary>Whether this transaction represents staked crypto (a staked outflow does not reduce the holding).</summary>
    public bool IsStaked { get; private set; }

    /// <summary>
    /// The exact instant (UTC) the transaction happened, when the source provides one. It orders
    /// same-day transactions and drives price lookups; <see cref="Date"/> is its UTC calendar date.
    /// </summary>
    public DateTime? OccurredAt { get; private set; }

    /// <summary>The source's own identifier (e.g. a blockchain transaction hash), when it has one.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>
    /// The stable identity of the imported row this transaction came from, built only from the
    /// source's immutable fields. Unique per account, so re-importing the same or an overlapping
    /// export never creates duplicates. Null for manually entered transactions.
    /// </summary>
    public string? ImportKey { get; private set; }

    /// <summary>When the transaction record was created.</summary>
    public DateTime CreatedAt { get; private set; }

    private readonly List<TransactionTag> _tags = new();

    /// <summary>The tags attached to this transaction.</summary>
    public IReadOnlyCollection<TransactionTag> Tags => _tags.AsReadOnly();

    private Transaction() { }

    /// <summary>Creates a new transaction, requiring a valid owning account.</summary>
    public static Transaction Create(int accountId, Symbol symbol, TransactionType type, Quantity quantity,
                                     decimal price, decimal fee, CurrencyCode currency, TradeDate date, string? notes,
                                     bool isStaked = false, DateTime? occurredAt = null, string? externalId = null,
                                     string? importKey = null)
    {
        if (accountId <= 0)
            throw new DomainException("A transaction must belong to an account.");
        return new Transaction
        {
            AccountId = accountId,
            Symbol = symbol,
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            Currency = currency,
            Date = date,
            Notes = notes ?? "",
            IsStaked = isStaked,
            OccurredAt = ToUtc(occurredAt),
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim(),
            ImportKey = string.IsNullOrWhiteSpace(importKey) ? null : importKey
        };
    }

    /// <summary>
    /// Links a transaction imported before import keys existed to the row it came from, correcting
    /// it to the row's exact values (older imports stored rounded doubles, local dates and a
    /// blockchain fee in the monetary fee field). User choices — staking, notes, tags — are kept.
    /// </summary>
    public void AdoptImport(string importKey, string? externalId, DateTime? occurredAt, TradeDate date,
                            Quantity quantity, decimal price, decimal fee)
    {
        ImportKey = importKey;
        ExternalId = string.IsNullOrWhiteSpace(externalId) ? ExternalId : externalId.Trim();
        OccurredAt = ToUtc(occurredAt) ?? OccurredAt;
        Date = date;
        Quantity = quantity;
        if (price != 0) Price = price;
        Fee = fee;
    }

    /// <summary>
    /// Refreshes an imported transaction whose source row changed its timing — e.g. a transaction
    /// exported while pending and again once confirmed. User choices are kept.
    /// </summary>
    public void RefreshImport(TradeDate date, DateTime? occurredAt, Quantity quantity, decimal price, decimal fee)
    {
        Date = date;
        OccurredAt = ToUtc(occurredAt);
        Quantity = quantity;
        Price = price;
        Fee = fee;
    }

    private static DateTime? ToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } v => v,
        { Kind: DateTimeKind.Local } v => v.ToUniversalTime(),
        var v => DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)
    };

    /// <summary>Updates the transaction's editable fields.</summary>
    public void Update(Symbol symbol, TransactionType type, Quantity quantity, decimal price, decimal fee,
                       CurrencyCode currency, TradeDate date, string? notes, bool isStaked = false)
    {
        Symbol = symbol;
        Type = type;
        Quantity = quantity;
        Price = price;
        Fee = fee;
        Currency = currency;
        Date = date;
        Notes = notes ?? "";
        IsStaked = isStaked;
    }

    /// <summary>Gross traded value = quantity × unit price, in the transaction currency.</summary>
    public Money GrossValue => new(Quantity.Value * Price, Currency);
}
