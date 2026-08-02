using Insgram_Manager.Automation.Models;
using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Automation.Queue;

/// <summary>
/// Abstraction for the job queue that manages pending automation tasks.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IQueueService
{
    Task<string> EnqueueAsync(AutomationType type, Dictionary<string, object> parameters);

    Task<QueuedJob?> DequeueAsync(CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string jobId);

    Task<int> GetPendingCountAsync();
}
