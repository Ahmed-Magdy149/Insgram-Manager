using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Data;

/// <summary>
/// Handles database initialization on application startup.
/// Ensures the SQLite database file and schema exist.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
