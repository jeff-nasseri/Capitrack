using Microsoft.JSInterop;

namespace Client.Infrastructure.Services;

/// <summary>
/// Theme handling — port of modules/theme.js (localStorage key wealth-theme).
/// Retained for parity with the original design; the live theme flow is now driven by
/// the Fluxor Theme feature (ThemeState / ToggleThemeAction).
/// </summary>
public class ThemeService(IJSRuntime js)
{
    public string Theme { get; private set; } = "dark";
    public event Action? OnChange;

    public async Task InitAsync()
    {
        Theme = await js.InvokeAsync<string>("capitrack.getTheme");
    }

    public async Task SetThemeAsync(string theme)
    {
        Theme = theme;
        await js.InvokeVoidAsync("capitrack.setTheme", theme);
        OnChange?.Invoke();
    }
}
