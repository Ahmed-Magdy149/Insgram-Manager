using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Provides automation job management: enqueue, pause, resume, retry, cancel,
/// and query job history. All operations are asynchronous and persisted.
/// </summary>
public interface IAutomationService
{
    /// <summary>
    /// Enqueues a new automation job and persists it to the database.
    /// Returns the generated job ID.
    /// </summary>
    Task<string> EnqueueJobAsync(AutomationType type, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Pauses a running or pending job.
    /// </summary>
    Task<bool> PauseJobAsync(string jobId);

    /// <summary>
    /// Resumes a paused job.
    /// </summary>
    Task<bool> ResumeJobAsync(string jobId);

    /// <summary>
    /// Retries a failed job.
    /// </summary>
    Task<bool> RetryJobAsync(string jobId);

    /// <summary>
    /// Cancels a pending, running, or paused job.
    /// </summary>
    Task<bool> CancelJobAsync(string jobId);

    /// <summary>
    /// Returns all automation jobs ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<AutomationJobDto>> GetAllJobsAsync();

    /// <summary>
    /// Returns jobs filtered by status.
    /// </summary>
    Task<IReadOnlyList<AutomationJobDto>> GetJobsByStatusAsync(JobStatus status);

    /// <summary>
    /// Returns a single job by ID, or null if not found.
    /// </summary>
    Task<AutomationJobDto?> GetJobAsync(string jobId);

    /// <summary>
    /// Returns aggregate statistics about automation jobs.
    /// </summary>
    Task<AutomationStatsDto> GetStatsAsync();
}

/// <summary>
/// DTO representing an automation job for the UI layer.
/// </summary>
public sealed record AutomationJobDto
{
    public string JobId { get; init; } = string.Empty;
    public AutomationType Type { get; init; }
    public JobStatus Status { get; init; }
    public DateTime QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResultMessage { get; init; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt - StartedAt : null;
}

/// <summary>
/// Aggregate statistics for the automation dashboard.
/// </summary>
public sealed record AutomationStatsDto
{
    public int TotalJobs { get; init; }
    public int PendingCount { get; init; }
    public int RunningCount { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }
    public int CancelledCount { get; init; }
    public int PausedCount { get; init; }
}
