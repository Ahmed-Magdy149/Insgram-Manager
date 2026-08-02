using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Insgram_Manager.Services;

/// <summary>
/// Scoped service that manages dark/light theme state via JS interop.
/// Persists the user's preference in localStorage and toggles the
/// Ant Design Blazor dark CSS link at runtime.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;

    public ThemeMode CurrentTheme { get; private set; } = ThemeMode.Light;

    public event Action? ThemeChanged;

    public ThemeService(IJSRuntime jsRuntime, NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var theme = await _jsRuntime.InvokeAsync<string>("themeManager.initTheme");
            CurrentTheme = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase)
                ? ThemeMode.Dark
                : ThemeMode.Light;
            ThemeChanged?.Invoke();
        }
        catch (JSDisconnectedException)
        {
            // Expected during prerendering or circuit teardown — safe to ignore.
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (prerendering) — safe to ignore.
        }
    }

    public async Task ToggleThemeAsync()
    {
        CurrentTheme = CurrentTheme == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        await ApplyThemeAsync();
    }

    public async Task SetThemeAsync(ThemeMode theme)
    {
        if (CurrentTheme == theme)
            return;

        CurrentTheme = theme;
        await ApplyThemeAsync();
    }

    private async Task ApplyThemeAsync()
    {
        var themeStr = CurrentTheme == ThemeMode.Dark ? "dark" : "light";

        try
        {
            await _jsRuntime.InvokeVoidAsync("themeManager.applyTheme", themeStr);
            await _jsRuntime.InvokeVoidAsync("themeManager.setStoredTheme", themeStr);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — state will sync on next connect.
        }
        catch (InvalidOperationException)
        {
            // JS interop unavailable — no-op.
        }

        ThemeChanged?.Invoke();
    }
}
