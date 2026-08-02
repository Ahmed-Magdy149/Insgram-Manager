using AutoMapper;
using FluentValidation;
using Insgram_Manager.Infrastructure;
using Insgram_Manager.Mapping;
using Insgram_Manager.Options;
using Insgram_Manager.Repositories;
using Insgram_Manager.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Infrastructure-layer service registration.
/// Registers persistence (UnitOfWork), file system, ZIP extraction, HTML parsing,
/// IOptions bindings, AutoMapper, and FluentValidation.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Registers all infrastructure services and cross-cutting concerns.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Persistence ---
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // --- File System ---
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // --- ZIP Extraction ---
        services.AddSingleton<IZipExtractionService, ZipExtractionService>();

        // --- HTML Parsing ---
        services.AddSingleton<IHtmlParser, HtmlParser>();

        // --- IOptions ---
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));
        services.Configure<AutomationOptions>(configuration.GetSection(AutomationOptions.SectionName));
        services.Configure<InstagramOptions>(configuration.GetSection(InstagramOptions.SectionName));

        // --- AutoMapper ---
        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

        // --- FluentValidation ---
        services.AddValidatorsFromAssemblyContaining<Validation.ImportRequestValidator>();

        return services;
    }
}
