using System.Globalization;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>
/// European Central Bank euro reference rates (FX). Official, free, no key; one rate per working
/// day since 1999, published around 16:00 CET. Every rate is "units of X per 1 EUR", so any pair is
/// derived through the euro: USD per GBP = (USD per EUR) / (GBP per EUR).
/// </summary>
public sealed class EcbProvider(HttpClient http, TimeProvider? clock = null, TimeSpan? rateLimit = null) : IPriceProvider
{
    private readonly RateGate _gate = new(rateLimit ?? TimeSpan.FromMilliseconds(300));

    public ProviderInfo Info { get; } = new(
        "ecb", "European Central Bank", [AssetClass.Fx], RequiresApiKey: false, ApiKeyEnvVar: null, ApiKeyUrl: null,
        Website: "https://data.ecb.europa.eu/help/api/overview",
        Limits: "Free, no key. Official euro reference rates for ~30 currencies, working days since 1999.");

    public bool IsConfigured => true;

    public bool Supports(string symbol, AssetClass assetClass) => assetClass == AssetClass.Fx && MarketSymbols.FxPair(symbol) is not null;

    /// <summary>Daily rates for a pair written like "EURUSD=X": units of the quote currency per 1 unit of the base.</summary>
    public async Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var pair = MarketSymbols.FxPair(symbol);
        if (pair is null) return null;
        var (b, q) = pair.Value;
        var perEurBase = b == "EUR" ? null : await PerEurAsync(b, from, to, ct);
        var perEurQuote = q == "EUR" ? null : await PerEurAsync(q, from, to, ct);
        IEnumerable<DateOnly> dates = (perEurBase ?? perEurQuote)?.Keys ?? Enumerable.Empty<DateOnly>();

        var closes = new List<DailyClose>();
        foreach (var d in dates.Where(d => d >= from && d <= to).Order())
        {
            decimal baseRate = perEurBase is null ? 1 : perEurBase.GetValueOrDefault(d);
            decimal quoteRate = perEurQuote is null ? 1 : perEurQuote.GetValueOrDefault(d);
            if (baseRate > 0 && quoteRate > 0) closes.Add(new DailyClose(d, quoteRate / baseRate));
        }
        return closes.Count == 0 ? null : new PriceSeries(symbol, q, Info.Id, closes);
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var series = await DailyAsync(symbol, today.AddDays(-10), today, ct);
        var last = series?.Closes.LastOrDefault();
        return last is null ? null : new QuoteDto { Symbol = symbol, Price = last.Close, Currency = series!.Currency, Name = symbol };
    }

    /// <summary>Units of <paramref name="currency"/> per 1 EUR, by date; empty when the ECB publishes nothing for the range.</summary>
    private async Task<Dictionary<DateOnly, decimal>> PerEurAsync(string currency, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        var url = $"https://data-api.ecb.europa.eu/service/data/EXR/D.{currency}.EUR.SP00.A?startPeriod={Iso(from)}&endPeriod={Iso(to)}&format=csvdata";
        using var response = await http.GetAsync(url, ct);
        var result = new Dictionary<DateOnly, decimal>();
        if ((int)response.StatusCode == 404) return result; // no observations (weekend/holiday-only range or unknown currency)
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"ECB {(int)response.StatusCode}");

        var lines = (await response.Content.ReadAsStringAsync(ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return result;
        var header = lines[0].Trim().Split(',');
        int dateCol = Array.IndexOf(header, "TIME_PERIOD"), valueCol = Array.IndexOf(header, "OBS_VALUE");
        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(','); // the first columns (key, dimensions, period, value) contain no commas
            if (cells.Length > Math.Max(dateCol, valueCol)
                && DateOnly.TryParseExact(cells[dateCol], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                && decimal.TryParse(cells[valueCol], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                result[d] = v;
        }
        return result;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
