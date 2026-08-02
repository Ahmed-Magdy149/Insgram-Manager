namespace Insgram_Manager.Constants;

/// <summary>
/// Constants for database configuration, table names, and migration metadata.
/// </summary>
public static class DatabaseConstants
{
    public const string DefaultConnectionString = "Data Source=insgram.db";

    public const string SettingsTableName = "Settings";

    public const int CommandTimeoutSeconds = 30;

    public const int MaxRetryCount = 3;

    public const int RetryDelayMs = 1000;
}
