using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Broadcasts automation job progress and status changes to connected clients via SignalR.
/// </summary>
public interface IAutomationProgressService
{
    Task NotifyJobQueuedAsync(string jobId, AutomationType type);

    Task NotifyJobStartedAsync(string jobId, AutomationType type);

    Task NotifyJobCompletedAsync(string jobId, AutomationType type, string? resultMessage);

    Task NotifyJobFailedAsync(string jobId, AutomationType type, string errorMessage);

    Task NotifyJobCancelledAsync(string jobId);

    Task NotifyJobPausedAsync(string jobId);

    Task NotifyJobResumedAsync(string jobId);

    Task NotifyJobRetryingAsync(string jobId, int retryCount, int maxRetries);
}
