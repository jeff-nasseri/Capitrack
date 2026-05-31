using Microsoft.AspNetCore.Components;

namespace Capitrack.Web.Services;

/// <summary>Lets each page drive the shared topbar (title, optional actions, optional back).</summary>
public class TopBarState
{
    public string Title { get; private set; } = "Dashboard";
    public string? Sub { get; private set; }
    public RenderFragment? Actions { get; private set; }
    public Action? OnBack { get; private set; }

    public event Action? OnChange;

    public void Set(string title, RenderFragment? actions = null, Action? onBack = null, string? sub = null)
    {
        Title = title;
        Actions = actions;
        OnBack = onBack;
        Sub = sub;
        OnChange?.Invoke();
    }
}
