namespace Insgram_Manager.Options;

/// <summary>
/// Configuration options for database connectivity and behavior.
/// Bound from the "Database" section in appsettings.json.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = "Data Source=insgram.db";

    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableSensitiveDataLogging { get; set; } = false;
}
