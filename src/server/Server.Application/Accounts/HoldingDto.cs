namespace Server.Application.Accounts;

/// <summary>API representation of a per-symbol holding for an account.</summary>
/// <param name="Symbol">The held symbol.</param>
/// <param name="Quantity">The net quantity held.</param>
/// <param name="AvgCost">The average cost per unit (average-cost method), or null when nothing is held.</param>
/// <param name="TotalCost">The cost basis of the units still held.</param>
/// <param name="TransactionCount">The number of contributing transactions.</param>
/// <param name="FirstTransaction">The date of the earliest contributing transaction.</param>
/// <param name="LastTransaction">The date of the latest contributing transaction.</param>
/// <param name="CostCurrency">The currency the cost basis is expressed in.</param>
public record HoldingDto(
    string Symbol, decimal Quantity, decimal? AvgCost, decimal TotalCost,
    int TransactionCount, string? FirstTransaction, string? LastTransaction, string? CostCurrency = null);
