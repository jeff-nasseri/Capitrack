namespace Client.Application.Store.Theme;

/// <summary>Loads the persisted theme from localStorage (capitrack.getTheme).</summary>
public sealed record InitThemeAction;

/// <summary>Sets the theme to an explicit value and persists it.</summary>
public sealed record SetThemeAction(string Theme);

/// <summary>Flips between "dark" and "light".</summary>
public sealed record ToggleThemeAction;
