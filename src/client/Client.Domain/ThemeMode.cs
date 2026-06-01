namespace Client.Domain;

/// <summary>
/// The two supported UI themes. The persisted/transported value is the lowercase
/// string ("dark"/"light") that matches the original V2 design and the data-theme
/// attribute; <see cref="ThemeModeExtensions"/> keeps that contract centralized.
/// </summary>
public enum ThemeMode
{
    Dark,
    Light
}
