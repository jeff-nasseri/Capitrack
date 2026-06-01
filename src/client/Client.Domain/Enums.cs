namespace Client.Domain;

/// <summary>
/// The two supported UI themes. The persisted/transported value is the lowercase
/// string ("dark"/"light") that matches the original V2 design and the data-theme
/// attribute; these helpers keep that contract centralized.
/// </summary>
public enum ThemeMode
{
    Dark,
    Light
}

public static class ThemeModeExtensions
{
    /// <summary>Wire value used in localStorage / the data-theme attribute.</summary>
    public static string ToValue(this ThemeMode mode) => mode == ThemeMode.Light ? "light" : "dark";

    /// <summary>Parse a wire value; anything other than "light" is treated as dark.</summary>
    public static ThemeMode Parse(string? value) =>
        string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? ThemeMode.Light : ThemeMode.Dark;
}
