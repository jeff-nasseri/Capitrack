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
    public double Price { get; private set; }

    /// <summary>Any transaction fee.</summary>
    public double Fee { get; private set; }

    /// <summary>The transaction currency.</summary>
    public CurrencyCode Currency { get; private set; } = CurrencyCode.Eur;

    /// <summary>The trade date.</summary>
    public TradeDate Date { get; private set; } = default!;

    /// <summary>Free-text notes.</summary>
    public string Notes { get; private set; } = "";

    /// <summary>Whether this transaction represents staked crypto (a staked outflow does not reduce the holding).</summary>
    public bool IsStaked { get; private set; }

    /// <summary>When the transaction record was created.</summary>
    public DateTime CreatedAt { get; private set; }

    private readonly List<TransactionTag> _tags = new();

    /// <summary>The tags attached to this transaction.</summary>
    public IReadOnlyCollection<TransactionTag> Tags => _tags.AsReadOnly();

    private Transaction() { }

    /// <summary>Creates a new transaction, requiring a valid owning account.</summary>
    public static Transaction Create(int accountId, Symbol symbol, TransactionType type, Quantity quantity,
                                     double price, double fee, CurrencyCode currency, TradeDate date, string? notes,
                                     bool isStaked = false)
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
            IsStaked = isStaked
        };
    }

    /// <summary>Updates the transaction's editable fields.</summary>
    public void Update(Symbol symbol, TransactionType type, Quantity quantity, double price, double fee,
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
