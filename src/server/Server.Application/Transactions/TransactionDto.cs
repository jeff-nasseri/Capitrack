using Server.Application.Tags;

namespace Server.Application.Transactions;

/// <summary>API representation of a transaction, including its account name and tags.</summary>
/// <param name="Id">The transaction's identifier.</param>
/// <param name="AccountId">The owning account's identifier.</param>
/// <param name="Symbol">The traded symbol.</param>
/// <param name="Type">The transaction type string.</param>
/// <param name="Quantity">The traded quantity.</param>
/// <param name="Price">The unit price.</param>
/// <param name="Fee">The transaction fee.</param>
/// <param name="Currency">The transaction currency code.</param>
/// <param name="Date">The trade date (yyyy-MM-dd).</param>
/// <param name="Notes">Free-text notes.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="AccountName">The owning account's name, if resolved.</param>
/// <param name="Tags">The tags attached to the transaction.</param>
public record TransactionDto(
    int Id, int AccountId, string Symbol, string Type, double Quantity, double Price,
    double Fee, string Currency, string Date, string Notes, DateTime CreatedAt,
    string? AccountName, List<TagDto> Tags);
