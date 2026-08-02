using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Data;

/// <summary>
/// Handles database initialization on application startup.
/// Recreates the SQLite database in development so schema changes are applied cleanly.
/// In production this should be replaced with migrations.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
