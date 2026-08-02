using AutoMapper;
using FluentValidation;
using Insgram_Manager.Mapping;
using Insgram_Manager.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Extension methods for registering application-level infrastructure:
/// IOptions binding, AutoMapper, and FluentValidation.
/// Keeps Program.cs clean and separates infrastructure concerns from service registration.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Registers all IOptions bindings from appsettings.json configuration sections.
    /// </summary>
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));
        services.Configure<AutomationOptions>(configuration.GetSection(AutomationOptions.SectionName));
        services.Configure<InstagramOptions>(configuration.GetSection(InstagramOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Registers AutoMapper with the application's mapping profiles.
    /// </summary>
    public static IServiceCollection AddAutoMapperProfiles(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

        return services;
    }

    /// <summary>
    /// Registers all FluentValidation validators from the application assembly.
    /// </summary>
    public static IServiceCollection AddFluentValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Validation.ImportRequestValidator>();

        return services;
    }
}
