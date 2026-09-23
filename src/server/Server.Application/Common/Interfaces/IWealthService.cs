namespace Server.Application.Common.Interfaces;

/// <summary>Portfolio aggregation, history and daily-wealth snapshots.</summary>
public interface IWealthService
{
    /// <summary>Computes the dashboard summary using live prices.</summary>
    Task<DashboardSummaryDto> DashboardSummaryAsync();

    /// <summary>Computes the portfolio value history for an optional account over a period.</summary>
    Task<List<PortfolioHistoryPointDto>> PortfolioHistoryAsync(int? accountId, string? period);

    /// <summary>Computes and stores today's wealth snapshot.</summary>
    Task<DailyWealthSnapshotDto> SaveDailyWealthAsync();

    /// <summary>Today's rates from each currency to the base currency, exactly as the dashboard converts.</summary>
    Task<ExchangeRatesDto> RatesToBaseAsync(IEnumerable<string> currencies);

    /// <summary>Returns stored daily wealth snapshots between two dates.</summary>
    Task<List<DailyWealthDto>> GetDailyWealthAsync(string start, string end);

    /// <summary>
    /// Tops up the cached daily closes and exchange rates the value history reads, so charts load from the
    /// cache instead of waiting on providers. Only the days not cached yet are fetched.
    /// </summary>
    Task WarmPriceCacheAsync(CancellationToken ct = default);
}
