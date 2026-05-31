using Capitrack.Api.Models;

namespace Capitrack.Api.Services;

/// <summary>
/// Pure holdings/aggregation math, ported 1:1 from the original SQL
/// (routes/accounts.ts holdings query and routes/prices.ts summary query).
/// Kept side-effect free so it can be unit-tested directly.
/// </summary>
public static class HoldingsCalculator
{
    private static readonly string[] AddTypes = ["buy", "transfer_in"];
    private static readonly string[] SubTypes = ["sell", "transfer_out"];

    /// <summary>Per-symbol holdings for one account (mirrors the /accounts/:id/holdings SQL).</summary>
    public static List<HoldingDto> AccountHoldings(IEnumerable<Transaction> transactions)
    {
        var result = new List<HoldingDto>();
        foreach (var g in transactions.GroupBy(t => t.Symbol))
        {
            double buyQty = g.Where(t => AddTypes.Contains(t.Type)).Sum(t => t.Quantity);
            double sellQty = g.Where(t => SubTypes.Contains(t.Type)).Sum(t => t.Quantity);
            double quantity = buyQty - sellQty;
            if (quantity <= 0.00000001) continue;

            double buyValue = g.Where(t => AddTypes.Contains(t.Type)).Sum(t => t.Quantity * t.Price);
            double? avgCost = buyQty != 0 ? buyValue / buyQty : null;
            double totalCost = g.Sum(t => t.Type == "buy" ? t.Quantity * t.Price + t.Fee
                : t.Type == "sell" ? -(t.Quantity * t.Price - t.Fee)
                : 0);

            result.Add(new HoldingDto(
                g.Key, quantity, avgCost, totalCost,
                g.Count(), g.Min(t => t.Date), g.Max(t => t.Date)));
        }
        // ORDER BY total_cost DESC
        return result.OrderByDescending(h => h.TotalCost).ToList();
    }

    public record AccountHolding(string Symbol, int AccountId, double Quantity, double AvgCost);

    /// <summary>Per-(symbol, account) net quantity + weighted avg cost (mirrors the dashboard summary SQL).</summary>
    public static List<AccountHolding> HoldingsByAccount(IEnumerable<Transaction> transactions)
    {
        var result = new List<AccountHolding>();
        foreach (var g in transactions.GroupBy(t => new { t.Symbol, t.AccountId }))
        {
            double buyQty = g.Where(t => AddTypes.Contains(t.Type)).Sum(t => t.Quantity);
            double sellQty = g.Where(t => SubTypes.Contains(t.Type)).Sum(t => t.Quantity);
            double quantity = buyQty - sellQty;
            if (quantity <= 0.00000001) continue;

            double buyValue = g.Where(t => AddTypes.Contains(t.Type)).Sum(t => t.Quantity * t.Price);
            double avgCost = buyQty != 0 ? buyValue / buyQty : 0;
            result.Add(new AccountHolding(g.Key.Symbol, g.Key.AccountId, quantity, avgCost));
        }
        return result;
    }
}
