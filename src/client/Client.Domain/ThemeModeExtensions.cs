namespace Client.Domain;

/// <summary>Helpers that convert <see cref="ThemeMode"/> to/from its lowercase wire value ("dark"/"light").</summary>
public static class ThemeModeExtensions
{
    /// <summary>Wire value used in localStorage / the data-theme attribute.</summary>
    public static string ToValue(this ThemeMode mode) => mode == ThemeMode.Light ? "light" : "dark";

    /// <summary>Parse a wire value; anything other than "light" is treated as dark.</summary>
    public static ThemeMode Parse(string? value) =>
        string.Equals(value, "light", StringComparison.OrdinalIgnoreCase) ? ThemeMode.Light : ThemeMode.Dark;
}
