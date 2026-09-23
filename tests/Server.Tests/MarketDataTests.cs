using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Application.Common.Interfaces;
using Server.Application.Prices;
using Server.Infrastructure.Services.MarketData;

namespace Server.Tests;

/// <summary>Price providers against recorded responses (no live calls), and the router's fallback/caching rules.</summary>
public class MarketDataTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan NoWait = TimeSpan.Zero;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Serves recorded responses by host and records every request.</summary>
    private sealed class Recorded : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public Dictionary<string, (HttpStatusCode Status, string File)> Routes { get; } = new()
        {
            ["api.coingecko.com"] = (HttpStatusCode.OK, "coingecko_market_chart_range_bitcoin.json"),
            ["api.kraken.com"] = (HttpStatusCode.OK, "kraken_ohlc_xbtusd.json"),
            ["api.bitvavo.com"] = (HttpStatusCode.OK, "bitvavo_candles_btc_eur.json"),
            ["api.twelvedata.com"] = (HttpStatusCode.OK, "twelvedata_time_series_aapl.json"),
            ["data-api.ecb.europa.eu"] = (HttpStatusCode.OK, "ecb_exr_usd.csv"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            var uri = request.RequestUri!;
            var (status, file) = Routes.TryGetValue(uri.Host + uri.AbsolutePath, out var exact) ? exact : Routes[uri.Host];
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(Fixture(file)) });
        }
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "prices", name));
    private static DateOnly D(int y, int m, int d) => new(y, m, d);
    private static (HttpClient Http, Recorded Api) Http() { var api = new Recorded(); return (new HttpClient(api), api); }

    // ---------------------------------------------------------------- providers on recorded responses

    [Fact]
    public async Task CoinGecko_turns_hourly_points_into_utc_daily_closes()
    {
        var (http, _) = Http();
        var series = await new CoinGeckoProvider(http, null, new FixedClock(Now), NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 20));

        series!.Currency.Should().Be("USD");
        series.Closes.Select(c => c.Date).Should().BeInAscendingOrder().And.OnlyContain(d => d >= D(2026, 1, 15) && d <= D(2026, 1, 20));
        // the close of Jan 15 is the price at 00:00 UTC on Jan 16
        using var doc = JsonDocument.Parse(Fixture("coingecko_market_chart_range_bitcoin.json"));
        var midnight = doc.RootElement.GetProperty("prices").EnumerateArray().First(p => p[0].GetInt64() == 1768521600000);
        series.Closes.Single(c => c.Date == D(2026, 1, 15)).Close.Should().Be(decimal.Parse(midnight[1].GetRawText(), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task CoinGecko_is_not_asked_for_history_older_than_its_365_day_window()
    {
        var (http, api) = Http();
        (await new CoinGeckoProvider(http, null, new FixedClock(Now), NoWait).DailyAsync("BTC-USD", D(2024, 3, 1), D(2024, 3, 5))).Should().BeNull();
        api.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CoinGecko_refusing_a_range_is_an_error_the_router_falls_back_from()
    {
        var (http, api) = Http();
        api.Routes["api.coingecko.com"] = (HttpStatusCode.Unauthorized, "coingecko_range_exceeded.json");
        var act = () => new CoinGeckoProvider(http, null, new FixedClock(Now), NoWait).DailyAsync("BTC-USD", D(2025, 9, 24), D(2025, 9, 30));
        (await act.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Contain("365 days");
    }

    [Fact]
    public void CoinGecko_only_prices_coins_it_can_identify_unambiguously()
    {
        var gecko = new CoinGeckoProvider(new HttpClient(), null);
        gecko.Supports("BTC-USD", AssetClass.Crypto).Should().BeTrue();
        gecko.Supports("MSVP-USD", AssetClass.Crypto).Should().BeFalse();   // unknown/spam token: no guessing by ticker
        gecko.Supports("YF-DAI-USD", AssetClass.Crypto).Should().BeFalse();
    }

    [Fact]
    public async Task Kraken_reads_its_own_pair_names_and_daily_candles()
    {
        var (http, api) = Http();
        var series = await new KrakenProvider(http, new FixedClock(Now), NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 20));

        api.Requests.Single().Query.Should().Contain("pair=XBTUSD");
        series!.Currency.Should().Be("USD");
        series.Closes.First().Should().Be(new DailyClose(D(2026, 1, 15), 95559.4m));
    }

    [Fact]
    public async Task Bitvavo_prices_in_euro_oldest_first()
    {
        var (http, api) = Http();
        var series = await new BitvavoProvider(http, NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 25));

        api.Requests.Single().AbsolutePath.Should().Be("/v2/BTC-EUR/candles");
        series!.Currency.Should().Be("EUR");
        series.Closes.Select(c => c.Date).Should().BeInAscendingOrder();
        series.Closes.Single(c => c.Date == D(2026, 1, 23)).Close.Should().Be(75721m);
    }

    [Fact]
    public async Task Twelve_data_needs_a_key_and_reads_exact_closes()
    {
        var (http, api) = Http();
        new TwelveDataProvider(http, null).IsConfigured.Should().BeFalse();

        var series = await new TwelveDataProvider(http, "test-key", NoWait).DailyAsync("AAPL", D(2026, 1, 15), D(2026, 1, 16));
        series!.Currency.Should().Be("USD");
        series.Closes.Should().Equal(new DailyClose(D(2026, 1, 15), 258.20999m), new DailyClose(D(2026, 1, 16), 255.53m));
        api.Requests.Single().Query.Should().Contain("symbol=AAPL");
    }

    [Fact]
    public async Task Ecb_rates_are_per_euro_and_invert_exactly()
    {
        var (http, _) = Http();
        var ecb = new EcbProvider(http, new FixedClock(Now), NoWait);
        (await ecb.DailyAsync("EURUSD=X", D(2026, 1, 12), D(2026, 1, 12)))!.Closes.Single().Close.Should().Be(1.1692m);
        (await ecb.DailyAsync("USDEUR=X", D(2026, 1, 12), D(2026, 1, 12)))!.Closes.Single().Close.Should().Be(1m / 1.1692m);
    }

    [Fact]
    public async Task Providers_receive_only_symbols_and_dates()
    {
        var (http, api) = Http();
        var clock = new FixedClock(Now);
        await new CoinGeckoProvider(http, null, clock, NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 20));
        await new KrakenProvider(http, clock, NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 20));
        await new BitvavoProvider(http, NoWait).DailyAsync("BTC-USD", D(2026, 1, 15), D(2026, 1, 20));
        await new TwelveDataProvider(http, "k", NoWait).DailyAsync("AAPL", D(2026, 1, 15), D(2026, 1, 16));
        await new EcbProvider(http, clock, NoWait).DailyAsync("EURUSD=X", D(2026, 1, 12), D(2026, 1, 16));

        string[] allowed = ["vs_currency", "from", "to", "pair", "interval", "since", "start", "end", "limit",
                            "symbol", "start_date", "end_date", "order", "outputsize", "apikey", "startPeriod", "endPeriod", "format"];
        foreach (var uri in api.Requests)
            System.Web.HttpUtility.ParseQueryString(uri.Query).AllKeys.Should().BeSubsetOf(allowed, uri.ToString());
    }

    [Fact]
    public async Task Kraken_quotes_many_pairs_in_one_request_under_its_own_pair_names()
    {
        var (http, api) = Http();
        api.Routes["api.kraken.com/0/public/AssetPairs"] = (HttpStatusCode.OK, "kraken_assetpairs.json");
        api.Routes["api.kraken.com/0/public/Ticker"] = (HttpStatusCode.OK, "kraken_ticker_multi.json");
        IPriceProvider kraken = new KrakenProvider(http, new FixedClock(Now), NoWait);

        var quotes = await kraken.QuotesAsync(["BTC-USD", "ETH-USD", "DOGE-USD", "MSVP-USD"]);

        quotes.Keys.Should().BeEquivalentTo("BTC-USD", "ETH-USD", "DOGE-USD");   // answered as XXBTZUSD, XETHZUSD, XDGUSD
        quotes["BTC-USD"].Price.Should().Be(85483.8m);
        quotes["DOGE-USD"].Price.Should().Be(0.0988656m);
        quotes["BTC-USD"].ChangePercent.Should().BeApproximately((double)((85483.8m - 86199.4m) / 86199.4m * 100), 1e-9);
        // an unlisted pair would make Kraken reject the whole request: it is never sent
        api.Requests.Single(r => r.AbsolutePath.EndsWith("/Ticker")).Query.Should().Be("?pair=XBTUSD,ETHUSD,XDGUSD");
    }

    [Fact]
    public async Task Bitvavo_quotes_every_market_from_one_request_in_euro()
    {
        var (http, api) = Http();
        api.Routes["api.bitvavo.com/v2/ticker/24h"] = (HttpStatusCode.OK, "bitvavo_ticker_24h.json");
        IPriceProvider bitvavo = new BitvavoProvider(http, NoWait);

        var quotes = await bitvavo.QuotesAsync(["BTC-USD", "ADA-USD", "DOGE-USD"]);

        api.Requests.Should().ContainSingle().Which.Query.Should().BeEmpty();
        quotes.Keys.Should().BeEquivalentTo("BTC-USD", "ADA-USD");                // DOGE is not in the recording
        quotes.Values.Should().OnlyContain(q => q.Currency == "EUR" && q.Price > 0);
    }

    [Fact]
    public async Task CoinGecko_quotes_every_coin_from_one_request()
    {
        var (http, api) = Http();
        api.Routes["api.coingecko.com/api/v3/simple/price"] = (HttpStatusCode.OK, "coingecko_simple_price.json");
        IPriceProvider gecko = new CoinGeckoProvider(http, null, new FixedClock(Now), NoWait);

        var quotes = await gecko.QuotesAsync(["BTC-USD", "ETH-USD"]);

        api.Requests.Should().ContainSingle().Which.Query.Should().Contain("ids=bitcoin,ethereum");
        quotes["BTC-USD"].Price.Should().Be(85611m);
        quotes["ETH-USD"].Price.Should().Be(2727.45m);
    }

    // ---------------------------------------------------------------- the router

    /// <summary>A scripted provider for routing tests.</summary>
    private sealed class FakeProvider(string id, Func<DateOnly, DateOnly, PriceSeries?>? daily = null, bool configured = true,
        PricePoint? hourly = null, AssetClass assetClass = AssetClass.Crypto, Dictionary<string, decimal>? prices = null,
        string currency = "USD") : IPriceProvider
    {
        public int DailyCalls;
        public List<string> Quoted { get; } = [];
        public ProviderInfo Info { get; } = new(id, id, [assetClass], false, null, null, "", "");
        public bool IsConfigured => configured;
        public bool Supports(string symbol, AssetClass c) => c == assetClass;
        public Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            DailyCalls++;
            return Task.FromResult(daily?.Invoke(from, to));
        }
        public Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default) => Task.FromResult(hourly);
        public Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
        {
            Quoted.Add(symbol);
            return Task.FromResult(prices?.TryGetValue(symbol, out var p) == true ? new QuoteDto { Symbol = symbol, Price = p, Currency = currency } : null);
        }
    }

    private static Func<DateOnly, DateOnly, PriceSeries?> Days(string id, decimal price, string currency = "USD", DateOnly? onlyFrom = null) =>
        (from, to) =>
        {
            var start = onlyFrom is { } f && f > from ? f : from;
            var closes = new List<DailyClose>();
            for (var d = start; d <= to; d = d.AddDays(1)) closes.Add(new DailyClose(d, price));
            return new PriceSeries("BTC-USD", currency, id, closes);
        };

    private static MarketDataService Router(TestDb t, params IPriceProvider[] providers) =>
        new(t.NewContext(), providers, NullLogger<MarketDataService>.Instance, new FixedClock(Now));

    // the default crypto order, whatever it is: tests script its first two providers
    private static readonly string First = MarketDataService.DefaultOrder[AssetClass.Crypto][0];
    private static readonly string Second = MarketDataService.DefaultOrder[AssetClass.Crypto][1];

    private static IPriceProvider[] Chain(IPriceProvider first, IPriceProvider second) =>
        [first, second, .. MarketDataService.DefaultOrder[AssetClass.Crypto].Skip(2).Select(id => new FakeProvider(id))];

    [Fact]
    public async Task A_failing_provider_falls_back_to_the_next_one()
    {
        using var t = new TestDb();
        var broken = new FakeProvider(First, (_, _) => throw new HttpRequestException("503"));
        var next = new FakeProvider(Second, Days(Second, 100m));
        var series = await Router(t, Chain(broken, next)).DailyAsync("BTC-USD", D(2026, 1, 1), D(2026, 1, 31));
        series!.Provider.Should().Be(Second);
        series.Closes.Should().HaveCount(31);
    }

    [Fact]
    public async Task A_provider_missing_part_of_the_range_falls_back_to_one_that_has_it()
    {
        using var t = new TestDb();
        var partial = new FakeProvider(First, Days(First, 1m, onlyFrom: D(2025, 9, 24)));   // e.g. a 365-day window
        var full = new FakeProvider(Second, Days(Second, 2m));
        var series = await Router(t, Chain(partial, full)).DailyAsync("BTC-USD", D(2025, 6, 1), D(2026, 1, 31));
        series!.Provider.Should().Be(Second);
    }

    [Fact]
    public async Task Past_closes_are_cached_and_never_fetched_twice()
    {
        using var t = new TestDb();
        var first = new FakeProvider(First, Days(First, 5m));
        await Router(t, Chain(first, new FakeProvider(Second))).DailyAsync("BTC-USD", D(2026, 1, 1), D(2026, 1, 31));
        var again = await Router(t, Chain(first, new FakeProvider(Second))).DailyAsync("BTC-USD", D(2026, 1, 10), D(2026, 1, 20));

        first.DailyCalls.Should().Be(1);
        again!.Closes.Should().HaveCount(11).And.OnlyContain(c => c.Close == 5m);
    }

    [Fact]
    public async Task Concurrent_requests_for_the_same_closes_fetch_them_once_and_both_succeed()
    {
        var file = Path.Combine(Path.GetTempPath(), $"capitrack-md-{Guid.NewGuid():N}.db");
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Server.Infrastructure.Persistence.CapitrackDbContext>()
            .UseSqlite($"Data Source={file};Pooling=False").Options;
        try
        {
            using (var db = new Server.Infrastructure.Persistence.CapitrackDbContext(options)) db.Database.EnsureCreated();
            var calls = 0;
            var slow = new FakeProvider(First, (from, to) => { Interlocked.Increment(ref calls); Thread.Sleep(150); return Days(First, 7m)(from, to); });
            MarketDataService NewRouter() => new(new Server.Infrastructure.Persistence.CapitrackDbContext(options), Chain(slow, new FakeProvider(Second)),
                NullLogger<MarketDataService>.Instance, new FixedClock(Now));

            var results = await Task.WhenAll(
                Task.Run(() => NewRouter().DailyAsync("BTC-USD", D(2026, 3, 1), D(2026, 3, 31))),
                Task.Run(() => NewRouter().DailyAsync("BTC-USD", D(2026, 3, 1), D(2026, 3, 31))));

            calls.Should().Be(1);
            results.Should().OnlyContain(r => r != null && r.Provider == First && r.Closes.Count == 31);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Extending_a_cached_range_fetches_only_the_missing_days()
    {
        using var t = new TestDb();
        var asked = new List<(DateOnly From, DateOnly To)>();
        var first = new FakeProvider(First, (from, to) => { asked.Add((from, to)); return Days(First, 5m)(from, to); });
        await Router(t, Chain(first, new FakeProvider(Second))).DailyAsync("BTC-USD", D(2026, 1, 10), D(2026, 1, 20));
        var wider = await Router(t, Chain(first, new FakeProvider(Second))).DailyAsync("BTC-USD", D(2026, 1, 1), D(2026, 1, 31));

        asked.Should().Equal((D(2026, 1, 10), D(2026, 1, 20)), (D(2026, 1, 1), D(2026, 1, 9)), (D(2026, 1, 21), D(2026, 1, 31)));
        wider!.Closes.Select(c => c.Date).Should().OnlyHaveUniqueItems().And.HaveCount(31);
    }

    [Fact]
    public async Task Todays_unfinished_close_is_never_cached()
    {
        using var t = new TestDb();
        var first = new FakeProvider(First, Days(First, 5m));
        var series = await Router(t, Chain(first, new FakeProvider(Second))).DailyAsync("BTC-USD", D(2026, 9, 1), D(2026, 9, 23));
        series!.Closes[^1].Date.Should().Be(D(2026, 9, 22));
    }

    [Fact]
    public async Task The_order_saved_in_settings_decides_the_provider()
    {
        using var t = new TestDb();
        var first = new FakeProvider(First, Days(First, 1m));
        var second = new FakeProvider(Second, Days(Second, 2m));
        await Router(t, Chain(first, second)).SetProviderOrderAsync(new() { [AssetClass.Crypto] = [Second, First] });

        (await Router(t, Chain(first, second)).DailyAsync("BTC-USD", D(2026, 2, 1), D(2026, 2, 5)))!.Provider.Should().Be(Second);
        first.DailyCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_provider_without_its_api_key_is_skipped()
    {
        using var t = new TestDb();
        var keyless = new FakeProvider(First, Days(First, 1m), configured: false);
        var second = new FakeProvider(Second, Days(Second, 2m));
        (await Router(t, Chain(keyless, second)).DailyAsync("BTC-USD", D(2026, 2, 1), D(2026, 2, 5)))!.Provider.Should().Be(Second);
        keyless.DailyCalls.Should().Be(0);
    }

    [Fact]
    public async Task Price_at_a_moment_prefers_the_hourly_candle_and_falls_back_to_the_daily_close()
    {
        using var t = new TestDb();
        var at = new DateTime(2026, 1, 21, 19, 15, 0, DateTimeKind.Utc);
        var hourly = new PricePoint(127.7m, "USD", First, "hour", at.AddMinutes(-15));
        (await Router(t, Chain(new FakeProvider(First, hourly: hourly), new FakeProvider(Second))).PriceAtAsync("SOL-USD", at))
            .Should().Be(hourly);

        var dailyOnly = Chain(new FakeProvider(First), new FakeProvider(Second, Days(Second, 130m)));
        var point = await Router(t, dailyOnly).PriceAtAsync("SOL-USD", at);
        point!.Resolution.Should().Be("day");
        point.Price.Should().Be(130m);
    }

    [Fact]
    public async Task Quotes_ask_each_provider_only_for_the_symbols_still_missing()
    {
        using var t = new TestDb();
        var first = new FakeProvider(First, prices: new() { ["BTC-USD"] = 100m });
        var second = new FakeProvider(Second, prices: new() { ["BTC-USD"] = 999m, ["ETH-USD"] = 50m });
        var stocks = new FakeProvider("yahoo-finance", assetClass: AssetClass.Stock, prices: new() { ["AAPL"] = 200m });

        var quotes = await Router(t, first, second, stocks).QuotesAsync(["btc-usd", "ETH-USD", "SPAM-USD", "AAPL"]);

        quotes.Keys.Should().BeEquivalentTo("BTC-USD", "ETH-USD", "AAPL");     // unpriced symbols are left out
        quotes["BTC-USD"].Price.Should().Be(100m);                           // the first provider wins
        second.Quoted.Should().BeEquivalentTo("ETH-USD", "SPAM-USD");        // never re-asked for what the first one had
    }

    [Fact]
    public async Task A_crypto_quote_priced_in_another_currency_is_shown_in_the_pairs_own_currency()
    {
        using var t = new TestDb();
        var euroOnly = new FakeProvider(First, prices: new() { ["BNB-USD"] = 500m }, currency: "EUR");   // e.g. Bitvavo
        var ecb = new FakeProvider("ecb", Days("ecb", 1.2m), assetClass: AssetClass.Fx);                 // 1 EUR = 1.2 USD

        var quote = (await Router(t, euroOnly, new FakeProvider(Second), ecb).QuotesAsync(["BNB-USD"]))["BNB-USD"];

        quote.Currency.Should().Be("USD");   // compared with a USD cost basis, never EUR against USD
        quote.Price.Should().Be(600m);
    }

    [Fact]
    public async Task Fx_uses_the_last_published_rate_on_or_before_the_date()
    {
        using var t = new TestDb();
        var (http, _) = Http();
        var ecb = new EcbProvider(http, new FixedClock(Now), NoWait);
        var router = Router(t, ecb, new FakeProvider("yahoo-finance", assetClass: AssetClass.Fx));

        (await router.FxRateAsync("EUR", "USD", D(2026, 1, 17))).Should().Be(1.1617m);   // Saturday → Friday's rate
        (await router.FxRateAsync("USD", "USD", D(2026, 1, 17))).Should().Be(1m);
    }
}
