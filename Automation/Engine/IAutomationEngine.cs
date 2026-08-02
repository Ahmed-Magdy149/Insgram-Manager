using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Automation.Engine;

/// <summary>
/// Abstraction for the automation engine that coordinates
/// queued jobs, worker execution, and strategy resolution.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IAutomationEngine
{
    Task<string> EnqueueJobAsync(AutomationType type, Dictionary<string, object> parameters);

    Task<bool> CancelJobAsync(string jobId);

    Task<JobStatus> GetJobStatusAsync(string jobId);

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
