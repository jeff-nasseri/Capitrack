namespace Server.Application.Prices.Queries;

/// <summary>Returns the price history for a symbol over a period.</summary>
/// <param name="Symbol">The symbol whose history to fetch.</param>
/// <param name="Period">The period key (e.g. 1w, 1m, 1y, max); defaults to 1y.</param>
public record GetHistoryQuery(string Symbol, string? Period) : IRequest<List<HistoryPointDto>>;

/// <summary>Handles <see cref="GetHistoryQuery"/>.</summary>
public sealed class GetHistoryQueryHandler(
    IMarketDataService market,
    IPriceService prices,
    IYahooFinanceClient yahoo,
    ILogger<GetHistoryQueryHandler> logger)
    : IRequestHandler<GetHistoryQuery, List<HistoryPointDto>>
{
    /// <summary>
    /// Daily closes from the configured providers (cached), in the currency the symbol is quoted in,
    /// ending with the latest quote. The one-week view keeps Yahoo's hourly candles when it has them.
    /// </summary>
    public async Task<List<HistoryPointDto>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetHistoryQuery));

        var symbol = request.Symbol.ToUpperInvariant();
        var period = string.IsNullOrEmpty(request.Period) ? "1y" : request.Period;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var days = new Dictionary<string, int> { ["1w"] = 7, ["1m"] = 30, ["3m"] = 90, ["6m"] = 180, ["1y"] = 365, ["5y"] = 1825 };
        var from = days.TryGetValue(period, out var d) ? today.AddDays(-d) : period == "max" ? new DateOnly(2000, 1, 1) : today.AddDays(-365);

        if (period == "1w")
        {
            try
            {
                var hourly = (await yahoo.ChartAsync(symbol, from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), "1h")).Where(p => p.Close != null).ToList();
                if (hourly.Count > 0) return hourly;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning("Hourly history for {Symbol} unavailable, using daily closes: {Error}", symbol, e.Message);
            }
        }

        var series = await market.DailyAsync(symbol, from, today, null, cancellationToken);
        var quote = await prices.GetCachedAsync(symbol);
        if (series is null)
            return quote is { Price: > 0 } q ? [new HistoryPointDto(DateTime.UtcNow, q.Price, null, null, null, null)] : [];

        // the chart sits next to the quote: show it in the quote's currency
        var currency = string.IsNullOrEmpty(quote?.Currency) ? series.Currency : quote!.Currency.ToUpperInvariant();
        IReadOnlyList<DailyClose> rates = [];
        if (currency != series.Currency)
            rates = (await market.DailyAsync($"{series.Currency}{currency}=X", from.AddDays(-10), today, null, cancellationToken))?.Closes ?? [];
        if (currency != series.Currency && rates.Count == 0) currency = series.Currency; // no rate: keep the provider's currency

        var closes = series.Closes;
        var step = closes.Count > 400 ? 7 : 1; // long ranges: weekly points
        var points = new List<HistoryPointDto>();
        for (var i = 0; i < closes.Count; i++)
        {
            if (i % step != 0 && i != closes.Count - 1) continue;
            var close = closes[i].Close * (currency == series.Currency ? 1m : RateOn(rates, closes[i].Date));
            points.Add(new HistoryPointDto(closes[i].Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), close, null, null, null, null));
        }
        if (quote is { Price: > 0 } latest && string.Equals(quote.Currency, currency, StringComparison.OrdinalIgnoreCase))
            points.Add(new HistoryPointDto(DateTime.UtcNow, latest.Price, null, null, null, null));
        return points;
    }

    /// <summary>The last rate published on or before the day (or the first one, for days before it).</summary>
    private static decimal RateOn(IReadOnlyList<DailyClose> rates, DateOnly day)
    {
        int lo = 0, hi = rates.Count - 1, found = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (rates[mid].Date <= day) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return rates[found].Close;
    }
}
