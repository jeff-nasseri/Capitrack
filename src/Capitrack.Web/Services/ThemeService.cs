using Microsoft.JSInterop;

namespace Capitrack.Web.Services;

/// <summary>Theme handling — port of modules/theme.js (localStorage key wealth-theme).</summary>
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
