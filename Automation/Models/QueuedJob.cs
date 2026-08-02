using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Automation.Models;

/// <summary>
/// Represents a queued automation job with type, parameters, and lifecycle metadata.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class QueuedJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public AutomationType Type { get; set; }

    public Dictionary<string, object> Parameters { get; set; } = [];

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetries { get; set; } = 3;
}
