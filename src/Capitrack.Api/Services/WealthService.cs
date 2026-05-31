using System.Globalization;
using System.Text.Json;
using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Api.Services;

public class WealthService(CapitrackDbContext db, PriceService prices, YahooFinanceClient yahoo)
{
    private static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string DaysAgo(int days) => DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<Dictionary<string, double>> RatesAsync()
    {
        var dict = new Dictionary<string, double>();
        foreach (var r in await db.CurrencyRates.ToListAsync())
            dict[$"{r.FromCurrency}_{r.ToCurrency}"] = r.Rate;
        return dict;
    }

    private async Task<string> BaseCurrencyAsync()
    {
        var user = await db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
        return user?.BaseCurrency ?? "EUR";
    }

    // ---- Dashboard summary (live prices) ----
    public async Task<DashboardSummaryDto> DashboardSummaryAsync()
    {
        var txs = await db.Transactions.ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var holdings = HoldingsCalculator.HoldingsByAccount(txs);

        var symbols = holdings.Select(h => h.Symbol).Distinct().ToList();
        var priceMap = new Dictionary<string, QuoteDto>();
        foreach (var s in symbols)
            priceMap[s] = await prices.GetQuoteAsync(s) ?? new QuoteDto { Symbol = s, Price = 0, Currency = "USD" };

        var rates = await RatesAsync();
        var baseCurrency = await BaseCurrencyAsync();

        var perAccount = new Dictionary<int, AccountAccum>();
        double totalWealth = 0, totalCost = 0;

        foreach (var h in holdings)
        {
            var price = priceMap.TryGetValue(h.Symbol, out var p) ? p : new QuoteDto { Price = 0, Currency = "USD" };
            double marketValue = h.Quantity * price.Price;
            double costBasis = h.Quantity * h.AvgCost;

            var priceCurrency = string.IsNullOrEmpty(price.Currency) ? "USD" : price.Currency;
            if (priceCurrency != baseCurrency)
                marketValue *= rates.GetValueOrDefault($"{priceCurrency}_{baseCurrency}", 1);

            var account = accounts.GetValueOrDefault(h.AccountId);
            var accountCurrency = account?.Currency ?? "";
            if (!string.IsNullOrEmpty(accountCurrency) && accountCurrency != baseCurrency)
                costBasis *= rates.GetValueOrDefault($"{accountCurrency}_{baseCurrency}", 1);

            if (!perAccount.TryGetValue(h.AccountId, out var acc))
                perAccount[h.AccountId] = acc = new AccountAccum { AccountId = h.AccountId, AccountName = account?.Name ?? "" };
            acc.MarketValue += marketValue;
            acc.CostBasis += costBasis;
            acc.HoldingsCount++;
            totalWealth += marketValue;
            totalCost += costBasis;
        }

        return new DashboardSummaryDto(
            totalWealth, totalCost, totalWealth - totalCost,
            totalCost > 0 ? (totalWealth - totalCost) / totalCost * 100 : 0,
            baseCurrency,
            perAccount.Values.Select(a => new AccountSummaryDto(a.AccountId, a.AccountName, a.MarketValue, a.CostBasis, a.HoldingsCount)).ToList(),
            holdings.Count);
    }

    private class AccountAccum
    {
        public int AccountId; public string AccountName = "";
        public double MarketValue; public double CostBasis; public int HoldingsCount;
    }

    // ---- Portfolio value history (transaction replay + historical prices, no FX) ----
    private static readonly string[] HistoryAdd = ["buy", "transfer_in", "dividend"];
    private static readonly string[] HistorySub = ["sell", "transfer_out"];

    public async Task<List<PortfolioHistoryPointDto>> PortfolioHistoryAsync(int? accountId, string? period)
    {
        var periodDays = new Dictionary<string, int?>
        {
            ["1w"] = 7, ["1m"] = 30, ["3m"] = 90, ["6m"] = 180, ["ytd"] = null,
            ["1y"] = 365, ["5y"] = 1825, ["all"] = 3650
        };
        var p = period ?? "3m";
        int days = (periodDays.TryGetValue(p, out var d) ? d : 90) ?? 90;
        string startDate = p == "ytd"
            ? new DateTime(DateTime.UtcNow.Year, 1, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DaysAgo(days);

        var q = db.Transactions.AsQueryable();
        if (accountId is int aid) q = q.Where(t => t.AccountId == aid);
        var transactions = await q.OrderBy(t => t.Date).ToListAsync();
        if (transactions.Count == 0) return [];

        var holdingMap = new Dictionary<string, double>();
        foreach (var tx in transactions)
        {
            holdingMap.TryAdd(tx.Symbol, 0);
            if (HistoryAdd.Contains(tx.Type)) holdingMap[tx.Symbol] += tx.Quantity;
            else if (HistorySub.Contains(tx.Type)) holdingMap[tx.Symbol] -= tx.Quantity;
        }
        var activeSymbols = holdingMap.Where(kv => kv.Value > 0.00000001).Select(kv => kv.Key).ToList();
        if (activeSymbols.Count == 0) return [];

        string interval = days <= 30 ? "1d" : "1wk";
        var start = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var priceHistories = new Dictionary<string, Dictionary<string, double>>();
        foreach (var symbol in activeSymbols)
        {
            var map = new Dictionary<string, double>();
            try
            {
                var chart = await yahoo.ChartAsync(symbol, start, interval);
                foreach (var pt in chart)
                    if (pt.Close is double cl)
                        map[pt.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = cl;
            }
            catch { /* fall through to cached fallback */ }
            if (map.Count == 0)
            {
                var cached = await prices.GetCachedAsync(symbol);
                map["fallback"] = cached?.Price ?? 0;
            }
            priceHistories[symbol] = map;
        }

        var allDates = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var kv in priceHistories)
            foreach (var dk in kv.Value.Keys)
                if (dk != "fallback") allDates.Add(dk);
        if (allDates.Count == 0) return [];

        var history = new List<PortfolioHistoryPointDto>();
        var running = new Dictionary<string, double>();
        int txIndex = 0;
        foreach (var dateStr in allDates)
        {
            while (txIndex < transactions.Count && string.CompareOrdinal(transactions[txIndex].Date, dateStr) <= 0)
            {
                var tx = transactions[txIndex];
                running.TryAdd(tx.Symbol, 0);
                if (HistoryAdd.Contains(tx.Type)) running[tx.Symbol] += tx.Quantity;
                else if (HistorySub.Contains(tx.Type)) running[tx.Symbol] -= tx.Quantity;
                txIndex++;
            }

            double totalValue = 0;
            foreach (var (symbol, qty) in running)
            {
                if (qty <= 0.00000001) continue;
                var ph = priceHistories.GetValueOrDefault(symbol) ?? [];
                double price;
                if (ph.TryGetValue(dateStr, out var exact)) price = exact;
                else
                {
                    var earlier = ph.Keys.Where(k => k != "fallback" && string.CompareOrdinal(k, dateStr) <= 0)
                                         .OrderBy(k => k, StringComparer.Ordinal).LastOrDefault();
                    price = earlier != null ? ph[earlier] : ph.GetValueOrDefault("fallback", 0);
                }
                totalValue += qty * price;
            }

            double totalCost = transactions
                .Where(t => string.CompareOrdinal(t.Date, dateStr) <= 0 && (t.Type == "buy" || t.Type == "transfer_in"))
                .Sum(t => t.Quantity * t.Price);

            history.Add(new PortfolioHistoryPointDto(
                dateStr,
                Math.Round(totalValue * 100) / 100,
                Math.Round(totalCost * 100) / 100,
                Math.Round((totalValue - totalCost) * 100) / 100));
        }
        return history;
    }

    // ---- Daily wealth snapshot (cached prices only) ----
    public async Task<object> SaveDailyWealthAsync()
    {
        var today = Today();
        var txs = await db.Transactions.ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var holdings = HoldingsCalculator.HoldingsByAccount(txs);

        var priceMap = new Dictionary<string, QuoteDto>();
        foreach (var s in holdings.Select(h => h.Symbol).Distinct())
        {
            var cached = await prices.GetCachedAsync(s);
            if (cached != null) priceMap[s] = cached;
        }

        var rates = await RatesAsync();
        var baseCurrency = await BaseCurrencyAsync();

        double totalWealth = 0, totalCost = 0;
        var details = new Dictionary<int, AccountAccum>();
        foreach (var h in holdings)
        {
            var price = priceMap.GetValueOrDefault(h.Symbol) ?? new QuoteDto { Price = 0, Currency = "USD" };
            double marketValue = h.Quantity * price.Price;
            double costBasis = h.Quantity * h.AvgCost;
            var priceCurrency = string.IsNullOrEmpty(price.Currency) ? "USD" : price.Currency;
            if (priceCurrency != baseCurrency)
                marketValue *= rates.GetValueOrDefault($"{priceCurrency}_{baseCurrency}", 1);
            var account = accounts.GetValueOrDefault(h.AccountId);
            var accountCurrency = account?.Currency ?? "";
            if (!string.IsNullOrEmpty(accountCurrency) && accountCurrency != baseCurrency)
                costBasis *= rates.GetValueOrDefault($"{accountCurrency}_{baseCurrency}", 1);

            if (!details.TryGetValue(h.AccountId, out var acc))
                details[h.AccountId] = acc = new AccountAccum { AccountId = h.AccountId, AccountName = account?.Name ?? "" };
            acc.MarketValue += marketValue;
            acc.CostBasis += costBasis;
            totalWealth += marketValue;
            totalCost += costBasis;
        }

        var detailsJson = JsonSerializer.Serialize(new
        {
            accounts = details.Values.Select(d => new { account_id = d.AccountId, name = d.AccountName, market_value = d.MarketValue, cost_basis = d.CostBasis }),
            holdings_count = holdings.Count
        });

        var row = await db.DailyWealth.FirstOrDefaultAsync(w => w.Date == today);
        if (row == null)
            db.DailyWealth.Add(new DailyWealth { Date = today, TotalWealth = totalWealth, TotalCost = totalCost, BaseCurrency = baseCurrency, Details = detailsJson, UpdatedAt = DateTime.UtcNow });
        else
        {
            row.TotalWealth = totalWealth; row.TotalCost = totalCost; row.BaseCurrency = baseCurrency; row.Details = detailsJson; row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        return new { date = today, total_wealth = totalWealth, total_cost = totalCost, base_currency = baseCurrency };
    }

    public async Task<List<object>> GetDailyWealthAsync(string start, string end)
    {
        var rows = await db.DailyWealth
            .Where(w => string.Compare(w.Date, start) >= 0 && string.Compare(w.Date, end) <= 0)
            .OrderBy(w => w.Date).ToListAsync();
        return rows.Select(r => (object)new
        {
            date = r.Date,
            total_wealth = r.TotalWealth,
            total_cost = r.TotalCost,
            base_currency = r.BaseCurrency,
            details = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrEmpty(r.Details) ? "{}" : r.Details)
        }).ToList();
    }
}
