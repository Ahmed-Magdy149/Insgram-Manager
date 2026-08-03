using Insgram_Manager.Domain.Enums;
using System.Text.Json;

namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents an automation job persisted in the database.
/// Tracks the full lifecycle: queued, running, paused, completed, failed, cancelled.
/// Stores parameters as JSON for flexibility across automation types.
/// </summary>
public class AutomationJob : BaseEntity
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");

    public AutomationType Type { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public string ParametersJson { get; set; } = "{}";

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 3;

    public string? ErrorMessage { get; set; }

    public string? ResultMessage { get; set; }

    /// <summary>
    /// Serializes parameters dictionary to JSON for storage.
    /// </summary>
    public static string SerializeParameters(Dictionary<string, object> parameters)
    {
        return JsonSerializer.Serialize(parameters);
    }

    /// <summary>
    /// Deserializes parameters from JSON storage.
    /// </summary>
    public Dictionary<string, object> DeserializeParameters()
    {
        return string.IsNullOrWhiteSpace(ParametersJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(ParametersJson) ?? [];
    }
}
