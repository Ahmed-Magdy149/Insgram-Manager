using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Data;

/// <summary>
/// Handles database initialization on application startup.
/// Applies EF Core migrations on startup so schema changes are managed safely.
/// In development and production this avoids deleting the database file.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        try
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }
}
