using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Options;
using Insgram_Manager.Services.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Registers Playwright-based Instagram authentication services.
/// Includes browser profile management, browser automation, and login orchestration.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BrowserAuthOptions>(configuration.GetSection(BrowserAuthOptions.SectionName));

        services.AddSingleton<IBrowserProfileService, BrowserProfileService>();
        services.AddSingleton<IInstagramBrowserService, InstagramBrowserService>();
        services.AddScoped<IInstagramLoginService, InstagramLoginService>();

        return services;
    }
}
