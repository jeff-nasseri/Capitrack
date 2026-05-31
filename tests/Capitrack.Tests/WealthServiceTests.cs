using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Capitrack.Tests;

// Ports tests/wealth-calculation.test.js — Wealth Calculation block (FX conversion).
public class WealthServiceTests
{
    private static WealthService BuildService(TestDb t)
    {
        var yahoo = new YahooFinanceClient(NullLogger<YahooFinanceClient>.Instance);
        var prices = new PriceService(t.Db, yahoo);
        return new WealthService(t.Db, prices, yahoo);
    }

    private static void Seed(TestDb t, string accountCurrency, string symbol, double qty, double buyPrice,
        double marketPrice, string priceCurrency)
    {
        t.Db.Users.Add(new User { Username = "test", PasswordHash = "x", BaseCurrency = "EUR" });
        t.Db.Accounts.Add(new Account { Name = "Acct", Type = "stock", Currency = accountCurrency });
        t.Db.SaveChanges();
        t.Db.Transactions.Add(new Transaction { AccountId = 1, Symbol = symbol, Type = "buy", Quantity = qty, Price = buyPrice, Currency = priceCurrency, Date = "2024-01-15" });
        // Fresh price_cache → PriceService serves it without calling Yahoo.
        t.Db.PriceCache.Add(new PriceCache { Symbol = symbol, Price = marketPrice, Currency = priceCurrency, Name = symbol, UpdatedAt = DateTime.UtcNow });
        t.Db.CurrencyRates.Add(new CurrencyRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.92 });
        t.Db.SaveChanges();
    }

    [Fact]
    public async Task Total_wealth_converts_USD_to_EUR()
    {
        using var t = new TestDb();
        Seed(t, "USD", "AAPL", 10, 150, 200, "USD");
        var summary = await BuildService(t).DashboardSummaryAsync();

        // 10 * 200 * 0.92 = 1840
        Assert.Equal(1840, summary.TotalWealth, 2);
        Assert.Equal("EUR", summary.BaseCurrency);
        Assert.True(summary.TotalGain > 0);
    }

    [Fact]
    public async Task Zero_price_yields_zero_wealth_and_negative_gain()
    {
        using var t = new TestDb();
        Seed(t, "USD", "AAPL", 10, 150, 0, "USD");
        var summary = await BuildService(t).DashboardSummaryAsync();
        Assert.Equal(0, summary.TotalWealth, 2);
        Assert.True(summary.TotalGain < 0);
    }

    [Fact]
    public async Task Missing_currency_rate_defaults_to_one()
    {
        using var t = new TestDb();
        Seed(t, "GBP", "AAPL", 10, 150, 200, "GBP"); // no GBP->EUR rate
        var summary = await BuildService(t).DashboardSummaryAsync();
        // No rate → factor 1 → 10 * 200 = 2000
        Assert.Equal(2000, summary.TotalWealth, 2);
    }
}
