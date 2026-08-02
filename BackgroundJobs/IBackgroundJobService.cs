using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.BackgroundJobs;

/// <summary>
/// Abstraction for scheduling and managing background jobs.
/// Prepared for Phase 2 — no implementation yet.
/// Will be used for scheduled Instagram automation tasks.
/// </summary>
public interface IBackgroundJobService
{
    Task<string> EnqueueAsync(string jobName, JobStatus priority = JobStatus.Pending);

    Task<bool> CancelAsync(string jobId);

    Task<JobStatus> GetStatusAsync(string jobId);
}
