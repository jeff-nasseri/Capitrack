using Server.Domain.Transactions;

namespace Server.Domain.Holdings;

/// <summary>
/// Pure holdings and cost-basis maths (domain service), side-effect free so it can be tested directly.
///
/// Positions are replayed in chronological order (instant, then date, then id) using the
/// average-cost method, per account and symbol:
/// <list type="bullet">
/// <item>buy: +quantity, cost += quantity × price + commission;</item>
/// <item>transfer in: +quantity, cost += the cost carried by the matching transfer out of another
/// (or the same) account — same source transaction id and symbol — so moving coins between your own
/// wallets is not a purchase; an unmatched transfer in (coins from outside Capitrack) is taken at
/// its value when received;</item>
/// <item>sell, transfer out, fee: −quantity and the cost of those units at the current average cost
/// (a partial sale leaves the average unchanged); a staked transfer out keeps the units;</item>
/// <item>dividend / interest: no change to units or cost.</item>
/// </list>
/// Quantities are exact decimals, so any positive balance — however small — is a holding.
/// </summary>
/// <summary>The cost basis remaining after a transaction, by the currency it was paid in.</summary>
public sealed record CostPoint(Transaction Transaction, IReadOnlyDictionary<string, decimal> CostByCurrency)
{
    public decimal TotalCost => CostByCurrency.Values.Sum();
}

public static class HoldingsCalculator
{
    /// <summary>The replayed state of one position.</summary>
    private sealed class Position
    {
        public decimal Quantity;
        public decimal Cost;
        public int Count;
        public string? First;
        public string? Last;
        public string? Currency;
    }

    /// <summary>Per-symbol holdings for one account's transactions (cost carried only between legs among them).</summary>
    public static IReadOnlyList<Holding> ForAccount(IEnumerable<Transaction> transactions) =>
        Replay(transactions)
            .Where(kv => kv.Value.Quantity > 0)
            .GroupBy(kv => kv.Key.Symbol)
            .Select(g => ToHolding(g.Key, g.Select(kv => kv.Value).ToList()))
            .OrderByDescending(h => h.TotalCost)
            .ToList();

    /// <summary>
    /// Per-symbol holdings of one account, replayed over the whole portfolio so a transfer between
    /// accounts carries its cost basis from the sending account.
    /// </summary>
    public static IReadOnlyList<Holding> ForAccount(IEnumerable<Transaction> portfolio, int accountId) =>
        Replay(portfolio)
            .Where(kv => kv.Key.AccountId == accountId && kv.Value.Quantity > 0)
            .Select(kv => ToHolding(kv.Key.Symbol, [kv.Value]))
            .OrderByDescending(h => h.TotalCost)
            .ToList();

    /// <summary>Per-(symbol, account) net quantity, average cost and cost basis, replayed over the portfolio.</summary>
    public static IReadOnlyList<AccountHolding> ByAccount(IEnumerable<Transaction> transactions) =>
        Replay(transactions)
            .Where(kv => kv.Value.Quantity > 0)
            .Select(kv => new AccountHolding(kv.Key.Symbol, kv.Key.AccountId, kv.Value.Quantity,
                kv.Value.Cost / kv.Value.Quantity, kv.Value.Currency))
            .ToList();

    /// <summary>
    /// Remaining cost basis after each transaction, in chronological order and by the currency it was
    /// paid in — the running "invested" line of a value-history chart. Uses the same rules as the holdings.
    /// </summary>
    public static IReadOnlyList<CostPoint> CostTimeline(IEnumerable<Transaction> transactions)
    {
        var timeline = new List<CostPoint>();
        Replay(transactions, (tx, positions) => timeline.Add(new CostPoint(tx, positions.Values
            .Where(p => p.Quantity > 0)
            .GroupBy(p => p.Currency ?? "")
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Cost)))));
        return timeline;
    }

    /// <summary>Chronological order: instant, then date, then id (sends before receipts at the same instant).</summary>
    public static IOrderedEnumerable<Transaction> Chronological(IEnumerable<Transaction> transactions) =>
        transactions
            .OrderBy(t => t.Date.Value, StringComparer.Ordinal)
            .ThenBy(t => t.OccurredAt ?? DateTime.MinValue)
            .ThenBy(t => t.Type.IncreasesQuantity ? 1 : 0)
            .ThenBy(t => t.Id);

    private static Dictionary<(Symbol Symbol, int AccountId), Position> Replay(
        IEnumerable<Transaction> transactions,
        Action<Transaction, Dictionary<(Symbol Symbol, int AccountId), Position>>? afterEach = null)
    {
        var list = transactions.ToList();
        var positions = new Dictionary<(Symbol, int), Position>();
        // source transaction ids that have a matching send, so their receipts carry cost instead of buying
        var sends = list.Where(t => t.Type == TransactionType.TransferOut && t.ExternalId != null)
                        .Select(t => (t.ExternalId!, t.Symbol)).ToHashSet();
        var carried = new Dictionary<(string, Symbol), Queue<decimal>>();

        foreach (var t in Chronological(list))
        {
            var key = (t.Symbol, t.AccountId);
            if (!positions.TryGetValue(key, out var p)) positions[key] = p = new Position();
            p.Count++;
            p.First ??= t.Date.Value;
            p.Last = t.Date.Value;
            var q = t.Quantity.Value;

            if (t.Type == TransactionType.Buy)
            {
                p.Currency ??= t.Currency.Value;
                p.Quantity += q;
                p.Cost += q * t.Price + t.Fee;
            }
            else if (t.Type == TransactionType.TransferIn)
            {
                p.Currency ??= t.Currency.Value;
                p.Quantity += q;
                var link = t.ExternalId is { } id ? (id, t.Symbol) : default;
                if (t.ExternalId != null && carried.TryGetValue(link, out var queue) && queue.Count > 0)
                    p.Cost += queue.Dequeue();                         // own-wallet transfer: basis moves with the coins
                else if (t.ExternalId == null || !sends.Contains(link))
                    p.Cost += q * t.Price + t.Fee;                     // received from outside Capitrack
            }
            else if (t.Type == TransactionType.TransferOut && t.IsStaked)
            {
                // staked: the units still belong to the holding
            }
            else if (t.Type.DecreasesQuantity) // sell, transfer out, fee
            {
                var removed = Dispose(p, q);
                if (t.Type == TransactionType.TransferOut && t.ExternalId is { } id)
                {
                    var link = (id, t.Symbol);
                    if (!carried.TryGetValue(link, out var queue)) carried[link] = queue = new Queue<decimal>();
                    queue.Enqueue(removed);
                }
            }
            afterEach?.Invoke(t, positions);
        }
        return positions;
    }

    /// <summary>Removes <paramref name="quantity"/> units at the average cost; returns the cost removed.</summary>
    private static decimal Dispose(Position p, decimal quantity)
    {
        decimal removed = 0;
        if (p.Quantity > 0)
        {
            var taken = Math.Min(quantity, p.Quantity);
            removed = p.Cost * taken / p.Quantity;
            p.Cost -= removed;
        }
        p.Quantity -= quantity;
        if (p.Quantity <= 0) p.Cost = 0;
        return removed;
    }

    private static Holding ToHolding(Symbol symbol, List<Position> parts)
    {
        var quantity = parts.Sum(p => p.Quantity);
        var cost = parts.Sum(p => p.Cost);
        return new Holding(
            symbol, quantity, quantity > 0 ? cost / quantity : null, cost,
            parts.Sum(p => p.Count),
            parts.Select(p => p.First).Where(d => d != null).Min(StringComparer.Ordinal),
            parts.Select(p => p.Last).Where(d => d != null).Max(StringComparer.Ordinal),
            parts.Select(p => p.Currency).FirstOrDefault(c => c != null));
    }
}
