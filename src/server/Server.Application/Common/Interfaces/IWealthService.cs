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

    /// <summary>Returns stored daily wealth snapshots between two dates.</summary>
    Task<List<DailyWealthDto>> GetDailyWealthAsync(string start, string end);
}
