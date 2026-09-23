namespace Server.Domain.Holdings;

/// <summary>Net position in a symbol within a specific account.</summary>
/// <param name="Symbol">The held symbol.</param>
/// <param name="AccountId">The owning account's identifier.</param>
/// <param name="Quantity">The net quantity held in the account.</param>
/// <param name="AvgCost">The average cost per unit (average-cost method).</param>
/// <param name="CostCurrency">The currency the cost is expressed in (that of the acquisitions).</param>
public sealed record AccountHolding(Symbol Symbol, int AccountId, decimal Quantity, decimal AvgCost, string? CostCurrency = null);
