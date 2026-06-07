namespace Server.Application.Transactions;

/// <summary>A user-selected transaction to import directly (no CSV parsing).</summary>
/// <param name="Symbol">The traded symbol.</param>
/// <param name="Type">The transaction type string.</param>
/// <param name="Quantity">The traded quantity.</param>
/// <param name="Price">The unit price.</param>
/// <param name="Fee">The transaction fee.</param>
/// <param name="Currency">The currency code.</param>
/// <param name="Date">The trade date (yyyy-MM-dd).</param>
/// <param name="Notes">Optional free-text notes.</param>
/// <param name="IsStaked">Whether this transaction represents staked crypto.</param>
public record SelectedTransactionDto(
    string Symbol, string Type, double Quantity, double Price, double Fee,
    string Currency, string Date, string? Notes, bool IsStaked);
