using Insgram_Manager.Import.Factories;
using Insgram_Manager.Import.Services;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Import module service registration.
/// Registers import-related services, parser factory, and progress service.
/// </summary>
public static class ImportExtensions
{
    /// <summary>
    /// Registers import module services.
    /// </summary>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        // --- Import Services ---
        services.AddScoped<IImportService, ImportService>();

        // --- Parser Factory ---
        services.AddSingleton<IImportParserFactory, ImportParserFactory>();

        // --- Import Progress (SignalR) ---
        services.AddSingleton<IImportProgressService, ImportProgressService>();

        return services;
    }
}
