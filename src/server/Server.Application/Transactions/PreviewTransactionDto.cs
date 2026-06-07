namespace Server.Application.Transactions;

/// <summary>A single parsed-but-not-yet-imported transaction row from an import file preview.</summary>
/// <param name="Index">The zero-based position of the row within its file.</param>
/// <param name="Symbol">The parsed symbol.</param>
/// <param name="Type">The parsed transaction type string.</param>
/// <param name="Quantity">The parsed quantity.</param>
/// <param name="Price">The parsed unit price.</param>
/// <param name="Fee">The parsed fee.</param>
/// <param name="Currency">The parsed currency code.</param>
/// <param name="Date">The parsed trade date (yyyy-MM-dd).</param>
/// <param name="Notes">The parsed notes.</param>
/// <param name="IsDuplicate">True when the row's fingerprint already exists in the target account.</param>
/// <param name="CanStake">True when the row may be flagged as staked (an outgoing crypto transfer).</param>
public record PreviewTransactionDto(
    int Index, string Symbol, string Type, double Quantity, double Price, double Fee,
    string Currency, string Date, string Notes, bool IsDuplicate, bool CanStake);
