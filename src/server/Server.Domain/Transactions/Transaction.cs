namespace Server.Domain.Transactions;

/// <summary>Join entity linking a <see cref="Transaction"/> to a tag.</summary>
public sealed class TransactionTag
{
    public int TransactionId { get; set; }
    public int TagId { get; set; }
}

/// <summary>A single trade/movement on an account. Aggregate root.</summary>
public sealed class Transaction : AggregateRoot<int>
{
    public int AccountId { get; private set; }
    public Symbol Symbol { get; private set; } = default!;
    public TransactionType Type { get; private set; } = TransactionType.Buy;
    public Quantity Quantity { get; private set; } = Quantity.Zero;
    public double Price { get; private set; }
    public double Fee { get; private set; }
    public CurrencyCode Currency { get; private set; } = CurrencyCode.Eur;
    public TradeDate Date { get; private set; } = default!;
    public string Notes { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private readonly List<TransactionTag> _tags = new();
    public IReadOnlyCollection<TransactionTag> Tags => _tags.AsReadOnly();

    private Transaction() { }

    public static Transaction Create(int accountId, Symbol symbol, TransactionType type, Quantity quantity,
                                     double price, double fee, CurrencyCode currency, TradeDate date, string? notes)
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
            Notes = notes ?? ""
        };
    }

    public void Update(Symbol symbol, TransactionType type, Quantity quantity, double price, double fee,
                       CurrencyCode currency, TradeDate date, string? notes)
    {
        Symbol = symbol;
        Type = type;
        Quantity = quantity;
        Price = price;
        Fee = fee;
        Currency = currency;
        Date = date;
        Notes = notes ?? "";
    }

    /// <summary>Gross traded value = quantity × unit price, in the transaction currency.</summary>
    public Money GrossValue => new(Quantity.Value * Price, Currency);
}
