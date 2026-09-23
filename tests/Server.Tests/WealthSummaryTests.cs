using System.Globalization;
using Server.Application.Common.Interfaces;
using Server.Application.Prices;
using Server.Application.Settings;
using Server.Domain.Transactions;
using Server.Infrastructure.Services;

namespace Server.Tests;

/// <summary>Live quotes: fetched together, cached, misses remembered; and the dashboard totals built from them.</summary>
public class WealthSummaryTests
{
    /// <summary>Quotes from a fixed table (USD); USD→EUR at 0.8; counts the batch requests.</summary>
    private sealed class FakeMarket(Dictionary<string, QuoteDto> quotes) : IMarketDataService
    {
        public List<List<string>> Batches { get; } = [];

        public Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
        {
            var asked = symbols.ToList();
            Batches.Add(asked);
            IReadOnlyDictionary<string, QuoteDto> found = asked.Where(quotes.ContainsKey).ToDictionary(s => s, s => new QuoteDto
            {
                Symbol = s, Price = quotes[s].Price, Currency = quotes[s].Currency, ChangePercent = quotes[s].ChangePercent
            });
            return Task.FromResult(found);
        }
        public Task<decimal?> FxRateAsync(string from, string to, DateOnly date, CancellationToken ct = default) =>
            Task.FromResult<decimal?>(from == "USD" && to == "EUR" ? 0.8m : null);
        public Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, string? providerId = null, CancellationToken ct = default) =>
            Task.FromResult<PriceSeries?>(null);
        public Task<PricePoint?> PriceAtAsync(string symbol, DateTime utc, string? providerId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MarketDataProviderDto>> ProvidersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetProviderOrderAsync(Dictionary<AssetClass, List<string>> order, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Uncached_quotes_are_fetched_in_one_batch_and_misses_are_not_retried_right_away()
    {
        using var t = new TestDb();
        var market = new FakeMarket(new() { ["QB-A-USD"] = new QuoteDto { Price = 10m, Currency = "USD" } });

        var first = await new PriceService(t.NewContext(), market).GetQuotesAsync(["qb-a-usd", "QB-MISSING-USD"]);
        var second = await new PriceService(t.NewContext(), market).GetQuotesAsync(["QB-A-USD", "QB-MISSING-USD"]);

        first.Keys.Should().Equal("QB-A-USD");
        second["QB-A-USD"].Price.Should().Be(10m);                         // from the 5-minute cache
        market.Batches.Should().ContainSingle().Which.Should().Equal("QB-A-USD", "QB-MISSING-USD");
    }

    [Fact]
    public async Task Dashboard_converts_with_the_ECB_rate_when_no_manual_rate_is_set_and_reports_the_days_change()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Wallet", "crypto", "USD");
        using (var db = t.NewContext())
        {
            db.Transactions.Add(Transaction.Create(account, Symbol.Create("QD-BTC-USD"), TransactionType.Buy, Quantity.Create(2), 50, 0,
                CurrencyCode.Usd, TradeDate.Create(DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), null));
            await db.SaveChangesAsync();
        }
        // +25% today: 2 × 100 now, 2 × 80 at the session start
        var market = new FakeMarket(new() { ["QD-BTC-USD"] = new QuoteDto { Price = 100m, Currency = "USD", ChangePercent = 25 } });
        using var ctx = t.NewContext();
        var summary = await new WealthService(ctx, new PriceService(ctx, market), market).DashboardSummaryAsync();

        summary.BaseCurrency.Should().Be("EUR");
        summary.TotalWealth.Should().Be(160m);   // 200 USD × 0.8, not 200 "EUR"
        summary.TotalCost.Should().Be(80m);      // 100 USD × 0.8
        summary.TodayChange.Should().Be(32m);    // (200 − 160) USD × 0.8
    }
}
