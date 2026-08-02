namespace Insgram_Manager.Options;

/// <summary>
/// Configuration options for the automation engine.
/// Bound from the "Automation" section in appsettings.json.
/// </summary>
public sealed class AutomationOptions
{
    public const string SectionName = "Automation";

    public int MaxConcurrentWorkers { get; set; } = 3;

    public int QueuePollingIntervalMs { get; set; } = 1000;

    public int MaxRetries { get; set; } = 3;

    public int RetryDelayMs { get; set; } = 5000;

    public bool Enabled { get; set; } = false;
}
