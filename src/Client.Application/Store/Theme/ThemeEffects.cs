using Fluxor;
using Microsoft.JSInterop;

namespace Client.Application.Store.Theme;

/// <summary>
/// Theme side effects: reads/writes the persisted theme through the same JS interop
/// helpers as the original ThemeService (capitrack.getTheme / capitrack.setTheme).
/// </summary>
public sealed class ThemeEffects(IJSRuntime js, IState<ThemeState> themeState)
{
    [EffectMethod(typeof(InitThemeAction))]
    public async Task OnInit(IDispatcher dispatcher)
    {
        var theme = await js.InvokeAsync<string>("capitrack.getTheme");
        dispatcher.Dispatch(new SetThemeAction(theme));
    }

    [EffectMethod]
    public async Task OnSetTheme(SetThemeAction action, IDispatcher dispatcher)
    {
        await js.InvokeVoidAsync("capitrack.setTheme", action.Theme);
    }

    [EffectMethod(typeof(ToggleThemeAction))]
    public Task OnToggle(IDispatcher dispatcher)
    {
        var next = themeState.Value.Theme == "dark" ? "light" : "dark";
        dispatcher.Dispatch(new SetThemeAction(next));
        return Task.CompletedTask;
    }
}
