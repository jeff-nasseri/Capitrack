using Server.Domain.Transactions;

namespace Server.Domain.Holdings;

/// <summary>A computed position in a single symbol (read model produced from transactions).</summary>
public sealed record Holding(
    Symbol Symbol,
    double Quantity,
    double? AvgCost,
    double TotalCost,
    int TransactionCount,
    string? FirstTransaction,
    string? LastTransaction);

/// <summary>Net position in a symbol within a specific account.</summary>
public sealed record AccountHolding(Symbol Symbol, int AccountId, double Quantity, double AvgCost);

/// <summary>
/// Pure holdings/aggregation math (domain service), ported 1:1 from the original
/// SQL. Side-effect free so it can be unit-tested directly.
/// </summary>
public static class HoldingsCalculator
{
    private const double Epsilon = 0.00000001;

    /// <summary>Per-symbol holdings for a set of transactions (one account).</summary>
    public static IReadOnlyList<Holding> ForAccount(IEnumerable<Transaction> transactions)
    {
        var result = new List<Holding>();
        foreach (var g in transactions.GroupBy(t => t.Symbol))
        {
            double buyQty = g.Where(t => t.Type.IncreasesQuantity).Sum(t => t.Quantity.Value);
            double sellQty = g.Where(t => t.Type.DecreasesQuantity).Sum(t => t.Quantity.Value);
            double quantity = buyQty - sellQty;
            if (quantity <= Epsilon) continue;

            double buyValue = g.Where(t => t.Type.IncreasesQuantity).Sum(t => t.Quantity.Value * t.Price);
            double? avgCost = buyQty != 0 ? buyValue / buyQty : null;
            double totalCost = g.Sum(t =>
                t.Type == TransactionType.Buy ? t.Quantity.Value * t.Price + t.Fee
                : t.Type == TransactionType.Sell ? -(t.Quantity.Value * t.Price - t.Fee)
                : 0);

            result.Add(new Holding(
                g.Key, quantity, avgCost, totalCost,
                g.Count(),
                g.Min(t => t.Date.Value),
                g.Max(t => t.Date.Value)));
        }
        return result.OrderByDescending(h => h.TotalCost).ToList();
    }

    /// <summary>Per-(symbol, account) net quantity + weighted average cost.</summary>
    public static IReadOnlyList<AccountHolding> ByAccount(IEnumerable<Transaction> transactions)
    {
        var result = new List<AccountHolding>();
        foreach (var g in transactions.GroupBy(t => new { t.Symbol, t.AccountId }))
        {
            double buyQty = g.Where(t => t.Type.IncreasesQuantity).Sum(t => t.Quantity.Value);
            double sellQty = g.Where(t => t.Type.DecreasesQuantity).Sum(t => t.Quantity.Value);
            double quantity = buyQty - sellQty;
            if (quantity <= Epsilon) continue;

            double buyValue = g.Where(t => t.Type.IncreasesQuantity).Sum(t => t.Quantity.Value * t.Price);
            double avgCost = buyQty != 0 ? buyValue / buyQty : 0;
            result.Add(new AccountHolding(g.Key.Symbol, g.Key.AccountId, quantity, avgCost));
        }
        return result;
    }
}
