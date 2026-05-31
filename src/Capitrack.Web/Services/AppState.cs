using Capitrack.Web.Models;

namespace Capitrack.Web.Services;

/// <summary>Global UI state — port of modules/state.js.</summary>
public class AppState
{
    public SessionDto? User { get; set; }
    public List<AccountDto> Accounts { get; set; } = [];
    public int? CurrentAccountId { get; set; }
    public string? CurrentSymbol { get; set; }
    public List<EnrichedHolding> AllHoldings { get; set; } = [];
    public DashboardSummaryDto? DashboardSummary { get; set; }

    public event Action? OnChange;
    public void NotifyChanged() => OnChange?.Invoke();
}
