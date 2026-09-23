using Server.Application.Common.Interfaces;
using Server.Application.Prices;
using Server.Application.Settings;
using Server.Application.Transactions;
using Server.Infrastructure.Services;

namespace Server.Tests;

/// <summary>Rows whose source gives no value are priced at the market price of their time.</summary>
public class ImportPricingTests
{
    /// <summary>Gold at 2,000 USD in the hour asked for (a different price on every call); 1 USD = 0.9 EUR.</summary>
    private sealed class FakeMarket : IMarketDataService
    {
        public int PriceCalls;
        public Task<PricePoint?> PriceAtAsync(string symbol, DateTime utc, string? providerId = null, CancellationToken ct = default)
        {
            PriceCalls++;
            var hour = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
            return Task.FromResult<PricePoint?>(new PricePoint(2000m + PriceCalls - 1, "USD", "fake", "hour", hour));
        }
        public Task<decimal?> FxRateAsync(string from, string to, DateOnly date, CancellationToken ct = default) =>
            Task.FromResult<decimal?>(from == "USD" && to == "EUR" ? 0.9m : null);
        public Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, string? providerId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, QuoteDto>> QuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MarketDataProviderDto>> ProvidersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetProviderOrderAsync(Dictionary<AssetClass, List<string>> order, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private const string Header = "Type,Product,Started Date,Completed Date,Description,Amount,Fee,Currency,State,Balance\n";

    [Fact]
    public async Task Revolut_commodity_exchanges_are_priced_at_their_hour_and_the_metal_fee_is_valued()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Gold", "commodity", "EUR");
        var csv = Header +
                  "EXCHANGE,Current,2026-03-10 09:15:00,2026-03-10 09:15:01,Exchanged to XAU,0.5,0.01,XAU,COMPLETED,0.5\n" +
                  "EXCHANGE,Current,2026-03-11 14:40:00,2026-03-11 14:40:01,Exchanged to EUR,-0.1,0.002,XAU,COMPLETED,0.4\n";
        var market = new FakeMarket();
        using (var db = t.NewContext()) await new ImporterService(db, market).ImportAsync(csv, account, null);

        using var check = t.NewContext();
        var txs = check.Transactions.OrderBy(x => x.Date).ToList();
        txs.Select(x => (x.Type.Value, x.Price, x.Fee, x.Currency.Value)).Should().Equal(
            ("buy", 1800m, 18m, "EUR"),       // 2,000 USD × 0.9; fee 0.01 oz × 1,800
            ("sell", 1800.9m, 3.60m, "EUR"));  // (2,000 + 1) USD × 0.9; fee 0.002 oz × 1,800.9 = 3.6018 → 3.60
        txs[0].Notes.Should().Contain("priced at the 2026-03-10 09:00 UTC hourly price (fake)");
    }

    [Fact]
    public async Task A_preview_and_the_import_after_it_use_the_same_market_price()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Gold", "commodity", "EUR");
        var csv = Header + "EXCHANGE,Current,2026-04-02 10:30:00,2026-04-02 10:30:01,Exchanged to XAU,0.25,0,XAU,COMPLETED,0.25\n";
        var market = new FakeMarket();
        var file = new ImportFileInput("gold.csv", csv);

        ImportPreviewDto preview;
        using (var db = t.NewContext()) preview = await new ImporterService(db, market).PreviewAsync(account, [file]);
        using (var db = t.NewContext()) await new ImporterService(db, market).ImportFilesAsync(account, [file]);

        using var check = t.NewContext();
        var previewed = preview.Files.Single().Rows.Single().Legs.Single().Price;
        check.Transactions.Single().Price.Should().Be(previewed).And.Be(1800m);
        market.PriceCalls.Should().Be(1);
    }

    [Fact]
    public async Task Trezor_amounts_worth_less_than_a_cent_keep_their_zero_value_and_are_not_looked_up()
    {
        using var t = new TestDb();
        var account = t.AddAccount("Wallet", "crypto", "USD");
        var csv = "Timestamp,Date,Time,Type,Transaction ID,Fee,Fee unit,Address,Label,Amount,Amount unit,Fiat (USD),Other\n" +
                  "1768000000,1/9/2026,11:06:40 PM GMT+1,RECV,dusttx1,,,addr1,,0.000001,XRP,0,\n";
        var market = new FakeMarket();
        using (var db = t.NewContext()) await new ImporterService(db, market).ImportAsync(csv, account, null);

        using var check = t.NewContext();
        check.Transactions.Single().Price.Should().Be(0m);
        market.PriceCalls.Should().Be(0);
    }
}
