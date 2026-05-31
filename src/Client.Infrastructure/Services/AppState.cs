using Client.Application.Models;

namespace Client.Infrastructure.Services;

/// <summary>
/// Transient UI state for accounts/holdings — port of modules/state.js. The
/// authenticated user now lives in the Fluxor SessionState; this service keeps the
/// per-session account/holdings caches the pages share.
/// </summary>
public class AppState
{
    public List<AccountDto> Accounts { get; set; } = [];
    public int? CurrentAccountId { get; set; }
    public string? CurrentSymbol { get; set; }
    public List<EnrichedHolding> AllHoldings { get; set; } = [];
    public DashboardSummaryDto? DashboardSummary { get; set; }

    public event Action? OnChange;
    public void NotifyChanged() => OnChange?.Invoke();
}
