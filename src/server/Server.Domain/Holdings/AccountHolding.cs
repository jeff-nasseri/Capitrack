namespace Server.Domain.Holdings;

/// <summary>Net position in a symbol within a specific account.</summary>
/// <param name="Symbol">The held symbol.</param>
/// <param name="AccountId">The owning account's identifier.</param>
/// <param name="Quantity">The net quantity held in the account.</param>
/// <param name="AvgCost">The weighted average buy cost.</param>
public sealed record AccountHolding(Symbol Symbol, int AccountId, double Quantity, double AvgCost);
