using Insgram_Manager.Interfaces;
using Insgram_Manager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Insgram_Manager.Extensions;

/// <summary>
/// Instagram communication module registration.
/// </summary>
public static class CommunicationExtensions
{
    public static IServiceCollection AddCommunication(this IServiceCollection services)
    {
        services.AddSingleton<IInstagramCommunicationProgressService, InstagramCommunicationProgressService>();
        services.AddSingleton<IInstagramRateLimiter, InstagramRateLimiter>();
        services.AddScoped<IInstagramSessionService, InstagramSessionService>();
        services.AddHttpClient<IInstagramRequestService, InstagramRequestService>();

        return services;
    }
}
