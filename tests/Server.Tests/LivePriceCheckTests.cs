using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Application.Common.Interfaces;
using Server.Infrastructure.Services;
using Server.Infrastructure.Services.MarketData;
using Xunit.Abstractions;

namespace Server.Tests;

/// <summary>
/// Compares each provider's price at a transaction's moment with the price recorded with the
/// transaction (its fiat value ÷ amount). This makes live calls, so it runs only when
/// <c>CAPITRACK_PRICE_SAMPLES</c> points at a local JSON file of <c>[{symbol, utc, price}]</c>
/// (kept outside the repo); otherwise it passes without doing anything.
/// </summary>
public class LivePriceCheckTests(ITestOutputHelper output)
{
    private sealed record Sample(string Symbol, DateTime Utc, decimal Price);

    [Fact]
    public async Task Provider_prices_agree_with_the_prices_recorded_at_transaction_time()
    {
        var path = Environment.GetEnvironmentVariable("CAPITRACK_PRICE_SAMPLES");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var samples = JsonSerializer.Deserialize<List<Sample>>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString })!;

        using var t = new TestDb();
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Capitrack/1.0 (self-hosted)");
        IPriceProvider[] providers =
        [
            new KrakenProvider(http), new BitvavoProvider(http),
            new CoinGeckoProvider(http, Environment.GetEnvironmentVariable("COINGECKO_DEMO_API_KEY")),
            new YahooProvider(new YahooFinanceClient(NullLogger<YahooFinanceClient>.Instance)), new EcbProvider(http)
        ];
        var market = new MarketDataService(t.NewContext(), providers, NullLogger<MarketDataService>.Instance);

        var agreed = 0;
        foreach (var s in samples)
        {
            var cells = new List<string>();
            var best = decimal.MaxValue;
            foreach (var id in new[] { "kraken", "bitvavo", "coingecko", "yahoo-finance" })
            {
                var point = await market.PriceAtAsync(s.Symbol, DateTime.SpecifyKind(s.Utc, DateTimeKind.Utc), id);
                if (point is null) { cells.Add($"{id}: -"); continue; }
                var usd = point.Price;
                if (point.Currency != "USD")
                    usd *= await market.FxRateAsync(point.Currency, "USD", DateOnly.FromDateTime(s.Utc)) ?? 0;
                var diff = s.Price == 0 ? 0 : (usd - s.Price) / s.Price * 100;
                best = Math.Min(best, Math.Abs(diff));
                cells.Add($"{id}: {diff.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)}% ({point.Resolution})");
            }
            if (best <= 1) agreed++;
            output.WriteLine($"{s.Symbol,-9} {s.Utc:yyyy-MM-dd HH:mm}Z  {string.Join("  ", cells)}");
        }
        output.WriteLine($"{agreed}/{samples.Count} samples within 1% of at least one provider");
        agreed.Should().BeGreaterThanOrEqualTo(10);
    }
}
