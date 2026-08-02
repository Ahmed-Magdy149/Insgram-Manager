namespace Insgram_Manager.Services;

/// <summary>
/// Application theme modes supported by the UI.
/// </summary>
public enum ThemeMode
{
    Light,
    Dark
}

/// <summary>
/// Manages the application's dark/light theme state.
/// Abstracted behind an interface for testability and SOLID compliance.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Current active theme mode.
    /// </summary>
    ThemeMode CurrentTheme { get; }

    /// <summary>
    /// Raised whenever the theme changes. Components subscribe to refresh UI.
    /// </summary>
    event Action? ThemeChanged;

    /// <summary>
    /// Initializes the theme from localStorage on first render.
    /// Must be called from OnAfterRenderAsync(firstRender: true).
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Toggles between light and dark themes.
    /// </summary>
    Task ToggleThemeAsync();

    /// <summary>
    /// Sets a specific theme mode.
    /// </summary>
    Task SetThemeAsync(ThemeMode theme);
}
