namespace Server.Application.Accounts;

/// <summary>API representation of a per-symbol holding for an account.</summary>
/// <param name="Symbol">The held symbol.</param>
/// <param name="Quantity">The net quantity held.</param>
/// <param name="AvgCost">The weighted average buy cost, or null when there are no buys.</param>
/// <param name="TotalCost">The total invested cost basis.</param>
/// <param name="TransactionCount">The number of contributing transactions.</param>
/// <param name="FirstTransaction">The date of the earliest contributing transaction.</param>
/// <param name="LastTransaction">The date of the latest contributing transaction.</param>
public record HoldingDto(
    string Symbol, double Quantity, double? AvgCost, double TotalCost,
    int TransactionCount, string? FirstTransaction, string? LastTransaction);
