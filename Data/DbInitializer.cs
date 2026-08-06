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
            await EnsureCoreSchemaAsync(context, logger);
            await EnsureMigrationHistoryAsync(context, logger);
            await context.Database.MigrateAsync();
            logger.LogInformation("Database schema verified and migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations.");
            throw;
        }
    }

    private static async Task EnsureCoreSchemaAsync(ApplicationDbContext context, ILogger logger)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Settings\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"Key\" TEXT NOT NULL, \"Value\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Settings_Key\" ON \"Settings\" (\"Key\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"InstagramAccounts\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InstagramAccounts\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"Username\" TEXT NOT NULL, \"FullName\" TEXT NULL, \"Biography\" TEXT NULL, \"ProfilePictureUrl\" TEXT NULL, \"FollowerCount\" INTEGER NOT NULL, \"FollowingCount\" INTEGER NOT NULL, \"PostCount\" INTEGER NOT NULL, \"IsVerified\" INTEGER NOT NULL, \"IsPrivate\" INTEGER NOT NULL, \"SourceFileName\" TEXT NULL, \"ImportSessionId\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramAccounts_Username\" ON \"InstagramAccounts\" (\"Username\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"InstagramRelationships\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InstagramRelationships\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"Username\" TEXT NOT NULL, \"FullName\" TEXT NULL, \"ProfileUrl\" TEXT NULL, \"RelationshipType\" INTEGER NOT NULL, \"SourceFileName\" TEXT NULL, \"ImportSessionId\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramRelationships_Username\" ON \"InstagramRelationships\" (\"Username\");");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramRelationships_RelationshipType\" ON \"InstagramRelationships\" (\"RelationshipType\");");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramRelationships_Username_RelationshipType\" ON \"InstagramRelationships\" (\"Username\", \"RelationshipType\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"ImportRecords\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_ImportRecords\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"SessionId\" TEXT NOT NULL, \"FileName\" TEXT NOT NULL, \"StartedAt\" TEXT NOT NULL, \"CompletedAt\" TEXT NULL, \"Status\" TEXT NOT NULL, \"TotalRecords\" INTEGER NOT NULL, \"ImportedRecords\" INTEGER NOT NULL, \"SkippedRecords\" INTEGER NOT NULL, \"FailedRecords\" INTEGER NOT NULL, \"AccountCount\" INTEGER NOT NULL, \"RelationshipCount\" INTEGER NOT NULL, \"ErrorMessage\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_ImportRecords_SessionId\" ON \"ImportRecords\" (\"SessionId\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"AutomationJobs\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_AutomationJobs\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"JobId\" TEXT NOT NULL, \"Type\" INTEGER NOT NULL, \"Status\" INTEGER NOT NULL, \"ParametersJson\" TEXT NOT NULL, \"QueuedAt\" TEXT NOT NULL, \"StartedAt\" TEXT NULL, \"CompletedAt\" TEXT NULL, \"RetryCount\" INTEGER NOT NULL, \"MaxRetries\" INTEGER NOT NULL, \"ErrorMessage\" TEXT NULL, \"ResultMessage\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_AutomationJobs_JobId\" ON \"AutomationJobs\" (\"JobId\");");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_AutomationJobs_Status\" ON \"AutomationJobs\" (\"Status\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"InstagramSessions\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InstagramSessions\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"AccountKey\" TEXT NOT NULL, \"Username\" TEXT NOT NULL, \"ProtectedAccessToken\" TEXT NULL, \"ProtectedCookieHeader\" TEXT NULL, \"ProtectedCsrfToken\" TEXT NULL, \"Notes\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_InstagramSessions_AccountKey\" ON \"InstagramSessions\" (\"AccountKey\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"InstagramInteractions\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InstagramInteractions\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"AccountKey\" TEXT NOT NULL, \"Action\" TEXT NOT NULL, \"Method\" TEXT NOT NULL, \"Endpoint\" TEXT NOT NULL, \"Summary\" TEXT NULL, \"Error\" TEXT NULL, \"RequestBody\" TEXT NULL, \"ResponseBody\" TEXT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramInteractions_AccountKey\" ON \"InstagramInteractions\" (\"AccountKey\");");
            await ExecuteNonQueryAsync(connection, "CREATE INDEX IF NOT EXISTS \"IX_InstagramInteractions_CreatedAt\" ON \"InstagramInteractions\" (\"CreatedAt\");");

            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"InstagramAuthSessions\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_InstagramAuthSessions\" PRIMARY KEY AUTOINCREMENT, \"CreatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"UpdatedAt\" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, \"AccountKey\" TEXT NOT NULL, \"Username\" TEXT NOT NULL, \"DisplayName\" TEXT NULL, \"ProfilePictureUrl\" TEXT NULL, \"ProfileDirectory\" TEXT NOT NULL, \"IsLoggedIn\" INTEGER NOT NULL, \"LastLoginAt\" TEXT NULL, \"LastValidatedAt\" TEXT NULL, \"RememberSession\" INTEGER NOT NULL);");
            await ExecuteNonQueryAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_InstagramAuthSessions_AccountKey\" ON \"InstagramAuthSessions\" (\"AccountKey\");");

            logger.LogInformation("Core database schema verified or repaired successfully.");
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static async Task EnsureMigrationHistoryAsync(ApplicationDbContext context, ILogger logger)
    {
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
            await connection.OpenAsync();

        try
        {
            await ExecuteNonQueryAsync(connection, "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $migrationId;";
            AddParameter(countCommand, "$migrationId", InitialMigrationId);
            var historyCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            if (historyCount > 0)
                return;

            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";
            AddParameter(command, "$migrationId", InitialMigrationId);
            AddParameter(command, "$productVersion", typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0");
            await command.ExecuteNonQueryAsync();

            logger.LogInformation("Seeded EF migrations history with the initial migration.");
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

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
