using System.Globalization;
using Server.Application.Common.Interfaces;
using Server.Application.Prices;
using Server.Application.Settings;
using Server.Domain.Transactions;
using Server.Infrastructure.Services;

namespace Server.Tests;

/// <summary>The value-history chart: every value and cost converted to the base currency at the day's rate.</summary>
public class PortfolioHistoryTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Constant closes per symbol; USD→EUR at 0.5 on past days and 0.8 today.</summary>
    private sealed class FakeMarket(Dictionary<string, decimal> closes) : IMarketDataService
    {
        public Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, string? providerId = null, CancellationToken ct = default)
        {
            var price = symbol == "USDEUR=X" ? 0.5m : closes.GetValueOrDefault(symbol);
            if (price == 0) return Task.FromResult<PriceSeries?>(null);
            var list = new List<DailyClose>();
            for (var d = from; d < to; d = d.AddDays(1)) list.Add(new DailyClose(d, price));
            return Task.FromResult<PriceSeries?>(new PriceSeries(symbol, symbol.EndsWith("=X") ? "EUR" : "USD", "fake", list));
        }
        public Task<decimal?> FxRateAsync(string from, string to, DateOnly date, CancellationToken ct = default) =>
            Task.FromResult<decimal?>(from == "USD" && to == "EUR" ? 0.8m : null);
        public Task<PricePoint?> PriceAtAsync(string symbol, DateTime utc, string? providerId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MarketDataProviderDto>> ProvidersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetProviderOrderAsync(Dictionary<AssetClass, List<string>> order, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoQuotes : IPriceService
    {
        public Task<QuoteDto?> GetQuoteAsync(string symbol) => Task.FromResult<QuoteDto?>(null);
        public Task<QuoteDto?> GetCachedAsync(string symbol) => Task.FromResult<QuoteDto?>(null);
        public Task UpsertAsync(QuoteDto quote) => Task.CompletedTask;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Values_and_costs_are_converted_to_the_base_currency_and_sold_holdings_still_count_before_their_sale()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Wallet", "crypto", "USD");
        var bought = Today.AddDays(-20);
        var sold = Today.AddDays(-10);
        using (var db = t.NewContext())
        {
            Transaction Tx(string symbol, TransactionType type, decimal qty, decimal price, DateOnly day) =>
                Transaction.Create(account, Symbol.Create(symbol), type, Quantity.Create(qty), price, 0,
                    CurrencyCode.Usd, TradeDate.Create(Iso(day)), null);
            db.Transactions.AddRange(
                Tx("BTC-USD", TransactionType.Buy, 2, 100, bought),
                Tx("ETH-USD", TransactionType.Buy, 1, 50, bought),
                Tx("ETH-USD", TransactionType.Sell, 1, 70, sold));
            await db.SaveChangesAsync();
        }

        using var ctx = t.NewContext();
        var wealth = new WealthService(ctx, new NoQuotes(), new FakeMarket(new() { ["BTC-USD"] = 100m, ["ETH-USD"] = 60m }));
        var history = await wealth.PortfolioHistoryAsync(null, "1m");

        // base currency EUR (no user): past days at 0.5 EUR per USD
        history[0].Should().Be(new PortfolioHistoryPointDto(Iso(bought), 130m, 125m, 5m));        // (2×100 + 1×60) × 0.5; cost 250 × 0.5
        history.Single(h => h.Date == Iso(sold)).Should().Be(new PortfolioHistoryPointDto(Iso(sold), 100m, 100m, 0m));
        // today at today's rate, the same one the dashboard uses
        history[^1].Should().Be(new PortfolioHistoryPointDto(Iso(Today), 160m, 160m, 0m));
    }

    [Fact]
    public async Task History_starts_at_the_first_transaction()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Wallet", "crypto", "USD");
        using (var db = t.NewContext())
        {
            db.Transactions.Add(Transaction.Create(account, Symbol.Create("BTC-USD"), TransactionType.Buy, Quantity.Create(1), 100, 0,
                CurrencyCode.Usd, TradeDate.Create(Iso(Today.AddDays(-3))), null));
            await db.SaveChangesAsync();
        }
        using var ctx = t.NewContext();
        var history = await new WealthService(ctx, new NoQuotes(), new FakeMarket(new() { ["BTC-USD"] = 100m })).PortfolioHistoryAsync(null, "all");
        history.Select(h => h.Date).Should().Equal(Iso(Today.AddDays(-3)), Iso(Today.AddDays(-2)), Iso(Today.AddDays(-1)), Iso(Today));
    }
}
