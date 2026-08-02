using Insgram_Manager.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Import module service registration.
/// Registers import-related interfaces as forward declarations.
/// Implementations will be added in Phase 2 when the Import Engine is built.
/// </summary>
public static class ImportExtensions
{
    /// <summary>
    /// Registers import module services.
    /// All registrations are forward declarations — no implementations exist yet.
    /// </summary>
    public static IServiceCollection AddImport(this IServiceCollection services)
    {
        // --- Import Services (Phase 2 forward declarations) ---
        // services.AddScoped<IParserFactory, ParserFactory>();
        // services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
