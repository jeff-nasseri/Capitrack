namespace Server.Domain.Holdings;

/// <summary>A computed position in a single symbol (read model produced from transactions).</summary>
/// <param name="Symbol">The held symbol.</param>
/// <param name="Quantity">The net quantity held.</param>
/// <param name="AvgCost">The average cost per unit (average-cost method), or null when nothing is held.</param>
/// <param name="TotalCost">The cost basis of the units still held.</param>
/// <param name="TransactionCount">The number of transactions contributing to the position.</param>
/// <param name="FirstTransaction">The date of the earliest contributing transaction.</param>
/// <param name="LastTransaction">The date of the latest contributing transaction.</param>
/// <param name="CostCurrency">The currency the cost basis is expressed in (that of the acquisitions).</param>
public sealed record Holding(
    Symbol Symbol,
    decimal Quantity,
    decimal? AvgCost,
    decimal TotalCost,
    int TransactionCount,
    string? FirstTransaction,
    string? LastTransaction,
    string? CostCurrency = null);
