namespace Insgram_Manager.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a background job.
/// Prepared for Phase 2 background job processing.
/// </summary>
public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Paused = 5,
    Retrying = 6
}
