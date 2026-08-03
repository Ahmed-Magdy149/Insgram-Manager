using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace Insgram_Manager.Data;

/// <summary>
/// Handles database initialization on application startup.
/// Applies EF Core migrations on startup so schema changes are managed safely.
/// In development and production this avoids deleting the database file.
/// </summary>
public static class DbInitializer
{
    private const string InitialMigrationId = "20260803000000_InitialCreate";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        try
        {
            await SeedLegacyMigrationHistoryIfNeededAsync(context, logger);
            await context.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }

    private static async Task SeedLegacyMigrationHistoryIfNeededAsync(ApplicationDbContext context, ILogger logger)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            var historyExists = await TableExistsAsync(connection, "__EFMigrationsHistory");
            if (!historyExists)
                return;

            var historyCount = await ScalarIntAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";");
            if (historyCount > 0)
                return;

            var legacySchemaExists = await TableExistsAsync(connection, "Settings")
                                     || await TableExistsAsync(connection, "InstagramAccounts")
                                     || await TableExistsAsync(connection, "ImportRecords");

            if (!legacySchemaExists)
                return;

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";
            AddParameter(command, "$migrationId", InitialMigrationId);
            AddParameter(command, "$productVersion", typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0");
            await command.ExecuteNonQueryAsync();

            logger.LogInformation("Seeded legacy EF migrations history with the initial migration.");
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        AddParameter(command, "$tableName", tableName);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<int> ScalarIntAsync(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
