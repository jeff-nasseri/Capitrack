namespace Server.Application.Settings;

/// <summary>A single transaction inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Id">The transaction's original identifier (remapped on import).</param>
/// <param name="AccountId">The owning account's original identifier (remapped on import).</param>
/// <param name="Symbol">The traded symbol.</param>
/// <param name="Type">The transaction type string.</param>
/// <param name="Quantity">The traded quantity.</param>
/// <param name="Price">The unit price.</param>
/// <param name="Fee">The transaction fee.</param>
/// <param name="Currency">The transaction currency code.</param>
/// <param name="Date">The trade date (yyyy-MM-dd).</param>
/// <param name="Notes">Free-text notes.</param>
/// <param name="TagIds">The original tag ids linked to the transaction (remapped on import).</param>
public record SnapshotTransaction(
    int Id,
    int AccountId,
    string Symbol,
    string Type,
    double Quantity,
    double Price,
    double Fee,
    string Currency,
    string Date,
    string? Notes,
    List<int> TagIds);
