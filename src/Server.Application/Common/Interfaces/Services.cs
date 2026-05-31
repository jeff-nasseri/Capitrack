using Server.Application.Common.Models;

namespace Server.Application.Common.Interfaces;

/// <summary>Commits tracked changes from the repositories in a single transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The currently authenticated user, resolved from the request context.</summary>
public interface ICurrentUser
{
    string? Username { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Password hashing/verification (BCrypt in the infrastructure layer).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Low-level Yahoo Finance access.</summary>
public interface IYahooFinanceClient
{
    Task<QuoteDto?> QuoteAsync(string symbol);
    Task<List<HistoryPointDto>> ChartAsync(string symbol, DateTime period1, string interval);
    Task<List<SearchResultDto>> SearchAsync(string query);
}

/// <summary>Quote retrieval with a 5-minute cache and stale fallback.</summary>
public interface IPriceService
{
    Task<QuoteDto?> GetQuoteAsync(string symbol);
    Task<QuoteDto?> GetCachedAsync(string symbol);
    Task UpsertAsync(QuoteDto quote);
}

/// <summary>Portfolio aggregation, history and daily-wealth snapshots.</summary>
public interface IWealthService
{
    Task<DashboardSummaryDto> DashboardSummaryAsync();
    Task<List<PortfolioHistoryPointDto>> PortfolioHistoryAsync(int? accountId, string? period);
    Task<DailyWealthSnapshotDto> SaveDailyWealthAsync();
    Task<List<DailyWealthDto>> GetDailyWealthAsync(string start, string end);
}

/// <summary>CSV format detection + multi-format transaction import with de-duplication.</summary>
public interface IImporterService
{
    DetectResultDto Detect(string content);
    Task<ImportResultDto> ImportAsync(string content, int accountId, string? formatHint);
}

/// <summary>Database path/state and persistence-related settings.</summary>
public interface ISystemService
{
    string DbPath { get; }
    bool DatabaseExists();
    (string Message, string Path, bool Exists) SetDatabasePath(string path);
}
