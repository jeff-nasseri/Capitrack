namespace Server.Domain.Transactions;

/// <summary>Join entity linking a <see cref="Transaction"/> to a tag.</summary>
public sealed class TransactionTag
{
    /// <summary>The linked transaction's identifier.</summary>
    public int TransactionId { get; set; }

    /// <summary>The linked tag's identifier.</summary>
    public int TagId { get; set; }
}
