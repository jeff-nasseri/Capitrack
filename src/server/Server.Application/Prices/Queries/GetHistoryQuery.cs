namespace Server.Application.Prices.Queries;

/// <summary>Returns the price history for a symbol over a period.</summary>
/// <param name="Symbol">The symbol whose history to fetch.</param>
/// <param name="Period">The period key (e.g. 1w, 1m, 1y, max); defaults to 1y.</param>
public record GetHistoryQuery(string Symbol, string? Period) : IRequest<List<HistoryPointDto>>;

/// <summary>Handles <see cref="GetHistoryQuery"/>.</summary>
public sealed class GetHistoryQueryHandler(
    IYahooFinanceClient yahoo,
    ILogger<GetHistoryQueryHandler> logger)
    : IRequestHandler<GetHistoryQuery, List<HistoryPointDto>>
{
    /// <summary>Maps the period key to a range/interval and fetches the history series.</summary>
    public async Task<List<HistoryPointDto>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(GetHistoryQuery));

        var symbol = request.Symbol.ToUpperInvariant();
        var period = string.IsNullOrEmpty(request.Period) ? "1y" : request.Period;

        var ranges = new Dictionary<string, DateTime>
        {
            ["1w"] = DateTime.UtcNow.AddDays(-7),
            ["1m"] = DateTime.UtcNow.AddDays(-30),
            ["3m"] = DateTime.UtcNow.AddDays(-90),
            ["6m"] = DateTime.UtcNow.AddDays(-180),
            ["1y"] = DateTime.UtcNow.AddDays(-365),
            ["5y"] = DateTime.UtcNow.AddDays(-1825),
            ["max"] = new DateTime(2000, 1, 1)
        };
        var period1 = ranges.GetValueOrDefault(period, ranges["1y"]);
        var interval = period == "1w" ? "1h" : period == "1m" ? "1d" : "1wk";

        var data = await yahoo.ChartAsync(symbol, period1, interval);
        return data.Where(d => d.Close != null).ToList();
    }
}
