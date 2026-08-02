using Insgram_Manager.Interfaces;
using Insgram_Manager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Application-layer service registration.
/// Registers all application services (logging, settings, notifications, loading, dialogs, theme).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application-level services.
    /// These are the services that the UI and application logic depend on.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // --- Logging ---
        services.AddSingleton<ILogService, LogService>();

        // --- Application Services ---
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<ILoadingService, LoadingService>();
        services.AddScoped<IDialogService, DialogService>();

        // --- Theme ---
        services.AddScoped<IThemeService, ThemeService>();

        return services;
    }
}
