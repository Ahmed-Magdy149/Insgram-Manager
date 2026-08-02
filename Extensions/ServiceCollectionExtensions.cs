using Insgram_Manager.Interfaces;
using Insgram_Manager.Repositories;
using Insgram_Manager.Repositories.Interfaces;
using Insgram_Manager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Centralized service registration for all application-level services.
/// Keeps Program.cs clean and follows SOLID (Single Responsibility).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application services, repositories, and the Unit of Work.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // --- Logging ---
        services.AddSingleton<ILogService, LogService>();

        // --- Application Services ---
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<ILoadingService, LoadingService>();
        services.AddScoped<IDialogService, DialogService>();

        // --- Repository & Unit of Work ---
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
