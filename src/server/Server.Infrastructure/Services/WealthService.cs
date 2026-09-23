using System.Globalization;
using System.Text.Json;
using Server.Domain.Accounts;
using Server.Domain.Holdings;
using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services;

/// <summary>
/// Portfolio aggregation, value history and daily-wealth snapshots.
/// Ported 1:1 from the original Capitrack.Api WealthService, with every domain
/// property access translated to the new value-object model.
/// </summary>
public sealed class WealthService(CapitrackDbContext db, IPriceService prices, IMarketDataService market) : IWealthService
{
    private static string Today() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Today's rate from each currency to the base currency, keyed "FROM_TO": the manual rate from
    /// settings (or the inverse of one) when there is one, otherwise the latest ECB reference rate.
    /// A currency with neither is left out (callers then keep the amount unconverted).
    /// </summary>
    private async Task<Dictionary<string, decimal>> RatesAsync(IEnumerable<string?> currencies, string baseCurrency)
    {
        var dict = new Dictionary<string, decimal>();
        foreach (var r in await db.CurrencyRates.ToListAsync())
            dict[$"{r.FromCurrency.Value}_{r.ToCurrency.Value}"] = r.Rate;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var currency in currencies.Where(c => !string.IsNullOrEmpty(c) && c != baseCurrency).Distinct())
        {
            var key = $"{currency}_{baseCurrency}";
            if (dict.ContainsKey(key)) continue;
            if (dict.TryGetValue($"{baseCurrency}_{currency}", out var inverse) && inverse > 0) dict[key] = 1m / inverse;
            else if (await market.FxRateAsync(currency!, baseCurrency, today) is { } live) dict[key] = live;
        }
        return dict;
    }

    private static string QuoteCurrency(QuoteDto q) => string.IsNullOrEmpty(q.Currency) ? "USD" : q.Currency;

    private static IEnumerable<string?> CostCurrencies(IEnumerable<AccountHolding> holdings, IReadOnlyDictionary<int, Account> accounts) =>
        holdings.Select(h => h.CostCurrency ?? accounts.GetValueOrDefault(h.AccountId)?.Currency.Value);

    private static IEnumerable<string?> CashCurrencies(IReadOnlyDictionary<int, Account> accounts) =>
        accounts.Values.Where(a => a.Type.IsCash).Select(a => (string?)a.Currency.Value);

    private async Task<string> BaseCurrencyAsync()
    {
        var user = await db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
        return user?.BaseCurrency.Value ?? "EUR";
    }

    // ---- Dashboard summary (live prices) ----
    public async Task<DashboardSummaryDto> DashboardSummaryAsync()
    {
        var txs = await db.Transactions.ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);

        // Cash/savings accounts are valued at their cash balance, never via a market quote,
        // so their currency "symbol" must never reach Yahoo: feed only non-cash txs to the calculator.
        var holdings = HoldingsCalculator.ByAccount(txs.Where(t => !IsCashAccount(accounts, t.AccountId)));

        var symbols = holdings.Select(h => h.Symbol.Value).Distinct().ToList();
        var priceMap = new Dictionary<string, QuoteDto>();
        foreach (var s in symbols)
            priceMap[s] = await prices.GetQuoteAsync(s) ?? new QuoteDto { Symbol = s, Price = 0, Currency = "USD" };

        var baseCurrency = await BaseCurrencyAsync();
        var rates = await RatesAsync(
            priceMap.Values.Select(QuoteCurrency).Concat(CostCurrencies(holdings, accounts)).Concat(CashCurrencies(accounts)), baseCurrency);

        var perAccount = new Dictionary<int, AccountAccum>();
        decimal totalWealth = 0, totalCost = 0;

        foreach (var h in holdings)
        {
            var price = priceMap.TryGetValue(h.Symbol.Value, out var p) ? p : new QuoteDto { Price = 0, Currency = "USD" };
            decimal marketValue = h.Quantity * price.Price;
            decimal costBasis = h.Quantity * h.AvgCost;

            var priceCurrency = string.IsNullOrEmpty(price.Currency) ? "USD" : price.Currency;
            if (priceCurrency != baseCurrency)
                marketValue *= rates.GetValueOrDefault($"{priceCurrency}_{baseCurrency}", 1m);

            var account = accounts.GetValueOrDefault(h.AccountId);
            // cost basis is expressed in the currency the units were acquired in (not the account's)
            var costCurrency = h.CostCurrency ?? account?.Currency.Value ?? "";
            if (!string.IsNullOrEmpty(costCurrency) && costCurrency != baseCurrency)
                costBasis *= rates.GetValueOrDefault($"{costCurrency}_{baseCurrency}", 1m);

            if (!perAccount.TryGetValue(h.AccountId, out var acc))
                perAccount[h.AccountId] = acc = new AccountAccum { AccountId = h.AccountId, AccountName = account?.Name ?? "" };
            acc.MarketValue += marketValue;
            acc.CostBasis += costBasis;
            acc.HoldingsCount++;
            totalWealth += marketValue;
            totalCost += costBasis;
        }

        // Cash/savings accounts: market value = balance × FX(accountCurrency→base); cost basis == market value (no gain).
        foreach (var cash in CashBalances(txs, accounts, baseCurrency, rates))
        {
            if (!perAccount.TryGetValue(cash.AccountId, out var acc))
                perAccount[cash.AccountId] = acc = new AccountAccum { AccountId = cash.AccountId, AccountName = cash.AccountName };
            acc.MarketValue += cash.Value;
            acc.CostBasis += cash.Value;
            totalWealth += cash.Value;
            totalCost += cash.Value;
        }

        return new DashboardSummaryDto(
            totalWealth, totalCost, totalWealth - totalCost,
            totalCost > 0 ? (totalWealth - totalCost) / totalCost * 100 : 0,
            baseCurrency,
            perAccount.Values.Select(a => new AccountSummaryDto(a.AccountId, a.AccountName, a.MarketValue, a.CostBasis, a.HoldingsCount)).ToList(),
            holdings.Count);
    }

    private static bool IsCashAccount(IReadOnlyDictionary<int, Account> accounts, int accountId) =>
        accounts.TryGetValue(accountId, out var a) && a.Type.IsCash;

    private readonly record struct CashAccountValue(int AccountId, string AccountName, decimal Value);

    /// <summary>
    /// Values each cash/savings account at balance × FX(accountCurrency→base).
    /// Balance = Σ (IncreasesQuantity ? +qty*price : DecreasesQuantity ? -qty*price : 0) over the account's transactions.
    /// Accounts with no transactions are skipped.
    /// </summary>
    private static IEnumerable<CashAccountValue> CashBalances(
        IEnumerable<Transaction> txs,
        IReadOnlyDictionary<int, Account> accounts,
        string baseCurrency,
        IReadOnlyDictionary<string, decimal> rates)
    {
        foreach (var g in txs.Where(t => IsCashAccount(accounts, t.AccountId)).GroupBy(t => t.AccountId))
        {
            var account = accounts.GetValueOrDefault(g.Key);
            decimal balance = g.Sum(t =>
                t.Type.IncreasesQuantity ? t.Quantity.Value * t.Price
                : t.Type.DecreasesQuantity ? -(t.Quantity.Value * t.Price)
                : 0);

            decimal value = balance;
            var accountCurrency = account?.Currency.Value ?? "";
            if (!string.IsNullOrEmpty(accountCurrency) && accountCurrency != baseCurrency)
                value *= rates.GetValueOrDefault($"{accountCurrency}_{baseCurrency}", 1m);

            yield return new CashAccountValue(g.Key, account?.Name ?? "", value);
        }
    }

    private class AccountAccum
    {
        public int AccountId; public string AccountName = "";
        public decimal MarketValue; public decimal CostBasis; public int HoldingsCount;
    }

    // ---- Portfolio value history (transaction replay x cached daily closes, in the base currency) ----
    private const decimal Dust = 0.00000001m;

    public async Task<List<PortfolioHistoryPointDto>> PortfolioHistoryAsync(int? accountId, string? period)
    {
        var periodDays = new Dictionary<string, int?>
        {
            ["1w"] = 7, ["1m"] = 30, ["3m"] = 90, ["6m"] = 180, ["ytd"] = null,
            ["1y"] = 365, ["5y"] = 1825, ["all"] = 3650
        };
        var p = period ?? "3m";
        int days = (periodDays.TryGetValue(p, out var d) ? d : 90) ?? 90;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = p == "ytd" ? new DateOnly(today.Year, 1, 1) : today.AddDays(-days);

        var q = db.Transactions.AsQueryable();
        if (accountId is int aid) q = q.Where(t => t.AccountId == aid);
        var transactions = HoldingsCalculator.Chronological(await q.ToListAsync()).ToList();
        if (transactions.Count == 0) return [];
        var firstDay = ParseDay(transactions[0].Date.Value);
        if (start < firstDay) start = firstDay; // nothing to value before the first transaction
        // remaining cost basis after each transaction (average cost; sales/fees remove cost, not proceeds)
        var costTimeline = HoldingsCalculator.CostTimeline(transactions);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);
        var baseCurrency = await BaseCurrencyAsync();

        // every market symbol held at some point in the window, including ones sold since
        var heldAtStart = new Dictionary<string, decimal>();
        var symbols = new HashSet<string>();
        foreach (var tx in transactions.Where(t => !IsCashAccount(accounts, t.AccountId)))
        {
            if (ParseDay(tx.Date.Value) >= start) symbols.Add(tx.Symbol.Value);
            else heldAtStart[tx.Symbol.Value] = heldAtStart.GetValueOrDefault(tx.Symbol.Value) + QuantityDelta(tx);
        }
        symbols.UnionWith(heldAtStart.Where(kv => kv.Value > Dust).Select(kv => kv.Key));

        // daily closes from the cache (fetched once from the providers), plus today's quote
        var lookback = start.AddDays(-7); // weekends and holidays: the first days take the last earlier close
        var series = new Dictionary<string, PriceSeries?>();
        var quotes = new Dictionary<string, QuoteDto>();
        foreach (var symbol in symbols)
        {
            series[symbol] = await market.DailyAsync(symbol, lookback, today);
            if (await prices.GetCachedAsync(symbol) is { Price: > 0 } quote) quotes[symbol] = quote;
        }
        var currencies = series.Values.Select(s => s?.Currency).Concat(quotes.Values.Select(QuoteCurrency))
            .Concat(costTimeline.SelectMany(c => c.CostByCurrency.Keys)).Concat(CashCurrencies(accounts))
            .Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList();
        var rates = await RatesAsync(currencies, baseCurrency);
        var fx = new Dictionary<string, Func<DateOnly, decimal>>();
        foreach (var currency in currencies)
            fx[currency] = await RateAtAsync(currency, baseCurrency, lookback, today, rates);
        decimal ToBase(decimal amount, string? currency, DateOnly day) =>
            string.IsNullOrEmpty(currency) || currency == baseCurrency ? amount : amount * fx[currency](day);

        // price of each symbol on a day, in the base currency: today's quote, else the last close on or before the day
        decimal PriceOn(string symbol, DateOnly day)
        {
            if (day == today && quotes.TryGetValue(symbol, out var live)) return ToBase(live.Price, QuoteCurrency(live), day);
            if (series[symbol] is { } s && LastOnOrBefore(s.Closes, day) is var i and >= 0) return ToBase(s.Closes[i].Close, s.Currency, day);
            return quotes.TryGetValue(symbol, out var fallback) ? ToBase(fallback.Price, QuoteCurrency(fallback), day) : 0;
        }

        var step = today.DayNumber - start.DayNumber <= 92 ? 1 : 7;
        var dates = new List<DateOnly>();
        for (var day = start; day < today; day = day.AddDays(step)) dates.Add(day);
        dates.Add(today);

        var history = new List<PortfolioHistoryPointDto>();
        var running = new Dictionary<string, decimal>();
        var cash = new Dictionary<int, decimal>();
        IReadOnlyDictionary<string, decimal> cost = new Dictionary<string, decimal>();
        int txIndex = 0;
        foreach (var day in dates)
        {
            var dateStr = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            while (txIndex < transactions.Count && string.CompareOrdinal(transactions[txIndex].Date.Value, dateStr) <= 0)
            {
                var tx = transactions[txIndex];
                cost = costTimeline[txIndex].CostByCurrency;
                if (IsCashAccount(accounts, tx.AccountId)) cash[tx.AccountId] = cash.GetValueOrDefault(tx.AccountId) + CashDelta(tx);
                else running[tx.Symbol.Value] = running.GetValueOrDefault(tx.Symbol.Value) + QuantityDelta(tx);
                txIndex++;
            }

            decimal totalValue = 0;
            foreach (var (symbol, qty) in running)
                if (qty > Dust) totalValue += qty * PriceOn(symbol, day);
            foreach (var (account, balance) in cash)
                totalValue += ToBase(balance, accounts.GetValueOrDefault(account)?.Currency.Value, day);
            var totalCost = cost.Sum(c => ToBase(c.Value, c.Key, day));

            history.Add(new PortfolioHistoryPointDto(
                dateStr,
                Math.Round(totalValue, 2),
                Math.Round(totalCost, 2),
                Math.Round(totalValue - totalCost, 2)));
        }
        return history;
    }

    private static decimal QuantityDelta(Transaction tx) =>
        tx.Type.IncreasesQuantity ? tx.Quantity.Value : tx.Type.DecreasesQuantity && !tx.IsStaked ? -tx.Quantity.Value : 0;

    private static decimal CashDelta(Transaction tx) =>
        tx.Type.IncreasesQuantity ? tx.Quantity.Value * tx.Price : tx.Type.DecreasesQuantity ? -(tx.Quantity.Value * tx.Price) : 0;

    /// <summary>
    /// The rate from <paramref name="currency"/> to the base currency on a day: the ECB reference rate of
    /// that day (or the last one before it) for past days, and today's rate (the one the dashboard uses)
    /// for today. Without a historical series every day takes today's rate.
    /// </summary>
    private async Task<Func<DateOnly, decimal>> RateAtAsync(string currency, string baseCurrency, DateOnly from, DateOnly today,
        IReadOnlyDictionary<string, decimal> todayRates)
    {
        if (currency == baseCurrency) return _ => 1m;
        var current = todayRates.GetValueOrDefault($"{currency}_{baseCurrency}", 1m);
        var series = await market.DailyAsync($"{currency}{baseCurrency}=X", from.AddDays(-10), today);
        if (series is null || series.Closes.Count == 0) return _ => current;
        var closes = series.Closes;
        return day =>
        {
            if (day >= today) return current;
            var i = LastOnOrBefore(closes, day);
            return i >= 0 ? closes[i].Close : closes[0].Close;
        };
    }

    /// <summary>Index of the last close dated on or before <paramref name="day"/> in an ascending list, or -1.</summary>
    private static int LastOnOrBefore(IReadOnlyList<DailyClose> closes, DateOnly day)
    {
        int lo = 0, hi = closes.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (closes[mid].Date <= day) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return found;
    }

    private static DateOnly ParseDay(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---- Daily wealth snapshot (cached prices only) ----
    public async Task<DailyWealthSnapshotDto> SaveDailyWealthAsync()
    {
        var today = Today();
        var txs = await db.Transactions.ToListAsync();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id);

        // Exclude cash/savings accounts from market-holding maths (valued at balance below).
        var holdings = HoldingsCalculator.ByAccount(txs.Where(t => !IsCashAccount(accounts, t.AccountId)));

        var priceMap = new Dictionary<string, QuoteDto>();
        foreach (var s in holdings.Select(h => h.Symbol.Value).Distinct())
        {
            var cached = await prices.GetCachedAsync(s);
            if (cached != null) priceMap[s] = cached;
        }

        var baseCurrency = await BaseCurrencyAsync();
        var rates = await RatesAsync(
            priceMap.Values.Select(QuoteCurrency).Concat(CostCurrencies(holdings, accounts)).Concat(CashCurrencies(accounts)), baseCurrency);

        decimal totalWealth = 0, totalCost = 0;
        var details = new Dictionary<int, AccountAccum>();
        foreach (var h in holdings)
        {
            var price = priceMap.GetValueOrDefault(h.Symbol.Value) ?? new QuoteDto { Price = 0, Currency = "USD" };
            decimal marketValue = h.Quantity * price.Price;
            decimal costBasis = h.Quantity * h.AvgCost;
            var priceCurrency = string.IsNullOrEmpty(price.Currency) ? "USD" : price.Currency;
            if (priceCurrency != baseCurrency)
                marketValue *= rates.GetValueOrDefault($"{priceCurrency}_{baseCurrency}", 1m);
            var account = accounts.GetValueOrDefault(h.AccountId);
            var costCurrency = h.CostCurrency ?? account?.Currency.Value ?? "";
            if (!string.IsNullOrEmpty(costCurrency) && costCurrency != baseCurrency)
                costBasis *= rates.GetValueOrDefault($"{costCurrency}_{baseCurrency}", 1m);

            if (!details.TryGetValue(h.AccountId, out var acc))
                details[h.AccountId] = acc = new AccountAccum { AccountId = h.AccountId, AccountName = account?.Name ?? "" };
            acc.MarketValue += marketValue;
            acc.CostBasis += costBasis;
            totalWealth += marketValue;
            totalCost += costBasis;
        }

        // Cash/savings accounts: valued at balance × FX(accountCurrency→base); cost basis == market value (no gain).
        foreach (var cash in CashBalances(txs, accounts, baseCurrency, rates))
        {
            if (!details.TryGetValue(cash.AccountId, out var acc))
                details[cash.AccountId] = acc = new AccountAccum { AccountId = cash.AccountId, AccountName = cash.AccountName };
            acc.MarketValue += cash.Value;
            acc.CostBasis += cash.Value;
            totalWealth += cash.Value;
            totalCost += cash.Value;
        }

        var detailsJson = JsonSerializer.Serialize(new
        {
            accounts = details.Values.Select(d => new { account_id = d.AccountId, name = d.AccountName, market_value = d.MarketValue, cost_basis = d.CostBasis }),
            holdings_count = holdings.Count
        });

        var row = await db.DailyWealth.FirstOrDefaultAsync(w => w.Date == today);
        if (row == null)
            db.DailyWealth.Add(new DailyWealthRecord { Date = today, TotalWealth = totalWealth, TotalCost = totalCost, BaseCurrency = baseCurrency, Details = detailsJson, UpdatedAt = DateTime.UtcNow });
        else
        {
            row.TotalWealth = totalWealth; row.TotalCost = totalCost; row.BaseCurrency = baseCurrency; row.Details = detailsJson; row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        return new DailyWealthSnapshotDto(today, totalWealth, totalCost, baseCurrency);
    }

    public async Task<List<DailyWealthDto>> GetDailyWealthAsync(string start, string end)
    {
        var rows = await db.DailyWealth
            .Where(w => string.Compare(w.Date, start) >= 0 && string.Compare(w.Date, end) <= 0)
            .OrderBy(w => w.Date).ToListAsync();
        return rows.Select(r => new DailyWealthDto(
            r.Date,
            r.TotalWealth,
            r.TotalCost,
            r.BaseCurrency,
            JsonSerializer.Deserialize<object>(string.IsNullOrEmpty(r.Details) ? "{}" : r.Details))).ToList();
    }
}
