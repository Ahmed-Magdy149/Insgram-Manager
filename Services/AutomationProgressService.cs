using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Hubs;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services;

/// <summary>
/// SignalR-based implementation of IAutomationProgressService.
/// Broadcasts automation job events to all connected clients.
/// </summary>
public sealed class AutomationProgressService : IAutomationProgressService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<AutomationProgressService> _logger;

    public AutomationProgressService(
        IHubContext<NotificationHub> hubContext,
        ILogger<AutomationProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyJobQueuedAsync(string jobId, AutomationType type)
    {
        _logger.LogInformation("Automation job queued: {JobId} — {Type}", jobId, type);
        await _hubContext.Clients.All.SendAsync("AutomationJobQueued", new { JobId = jobId, Type = type.ToString() });
    }

    public async Task NotifyJobStartedAsync(string jobId, AutomationType type)
    {
        _logger.LogInformation("Automation job started: {JobId} — {Type}", jobId, type);
        await _hubContext.Clients.All.SendAsync("AutomationJobStarted", new { JobId = jobId, Type = type.ToString() });
    }

    public async Task NotifyJobCompletedAsync(string jobId, AutomationType type, string? resultMessage)
    {
        _logger.LogInformation("Automation job completed: {JobId} — {Type} — {Message}", jobId, type, resultMessage);
        await _hubContext.Clients.All.SendAsync("AutomationJobCompleted", new
        {
            JobId = jobId,
            Type = type.ToString(),
            ResultMessage = resultMessage
        });
    }

    public async Task NotifyJobFailedAsync(string jobId, AutomationType type, string errorMessage)
    {
        _logger.LogError("Automation job failed: {JobId} — {Type} — {Error}", jobId, type, errorMessage);
        await _hubContext.Clients.All.SendAsync("AutomationJobFailed", new
        {
            JobId = jobId,
            Type = type.ToString(),
            Error = errorMessage
        });
    }

    public async Task NotifyJobCancelledAsync(string jobId)
    {
        _logger.LogInformation("Automation job cancelled: {JobId}", jobId);
        await _hubContext.Clients.All.SendAsync("AutomationJobCancelled", new { JobId = jobId });
    }

    public async Task NotifyJobPausedAsync(string jobId)
    {
        _logger.LogInformation("Automation job paused: {JobId}", jobId);
        await _hubContext.Clients.All.SendAsync("AutomationJobPaused", new { JobId = jobId });
    }

    public async Task NotifyJobResumedAsync(string jobId)
    {
        _logger.LogInformation("Automation job resumed: {JobId}", jobId);
        await _hubContext.Clients.All.SendAsync("AutomationJobResumed", new { JobId = jobId });
    }

    public async Task NotifyJobRetryingAsync(string jobId, int retryCount, int maxRetries)
    {
        _logger.LogWarning("Automation job retrying: {JobId} — Attempt {RetryCount}/{MaxRetries}", jobId, retryCount, maxRetries);
        await _hubContext.Clients.All.SendAsync("AutomationJobRetrying", new
        {
            JobId = jobId,
            RetryCount = retryCount,
            MaxRetries = maxRetries
        });
    }
}
