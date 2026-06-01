namespace Server.Domain.Accounts;

/// <summary>Join entity linking an <see cref="Account"/> to a tag.</summary>
public sealed class AccountTag
{
    /// <summary>The linked account's identifier.</summary>
    public int AccountId { get; set; }

    /// <summary>The linked tag's identifier.</summary>
    public int TagId { get; set; }
}
