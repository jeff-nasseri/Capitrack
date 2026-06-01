namespace Server.Application.Prices.Queries;

public record GetHistoryQuery(string Symbol, string? Period) : IRequest<List<HistoryPointDto>>;

public sealed class GetHistoryQueryHandler(IYahooFinanceClient yahoo)
    : IRequestHandler<GetHistoryQuery, List<HistoryPointDto>>
{
    public async Task<List<HistoryPointDto>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
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
