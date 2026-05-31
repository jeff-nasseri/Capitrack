using Fluxor;

namespace Client.Application.Store.Theme;

/// <summary>The active UI theme as the wire value ("dark"/"light"). Defaults to "dark".</summary>
[FeatureState]
public record ThemeState
{
    public string Theme { get; init; } = "dark";

    // Fluxor initial-state constructor (defaults to the dark theme).
    private ThemeState() { }

    public ThemeState(string theme) => Theme = theme;
}
