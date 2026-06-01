using Fluxor;

namespace Client.Application.Store.Theme;

/// <summary>Pure reducers for the theme feature.</summary>
public static class ThemeReducers
{
    [ReducerMethod(typeof(InitThemeAction))]
    public static ThemeState OnInit(ThemeState state) => state;

    [ReducerMethod]
    public static ThemeState OnSetTheme(ThemeState state, SetThemeAction action)
        => state with { Theme = action.Theme };

    [ReducerMethod(typeof(ToggleThemeAction))]
    public static ThemeState OnToggle(ThemeState state) => state;
}
