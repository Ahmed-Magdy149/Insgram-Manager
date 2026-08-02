using Insgram_Manager.Hubs;
using Insgram_Manager.Import.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services;

/// <summary>
/// Service for broadcasting import progress updates to connected clients via SignalR.
/// Used by ImportService to push real-time progress, statistics, and completion events.
/// </summary>
public interface IImportProgressService
{
    Task NotifyProgressAsync(string sessionId, ImportStatistics statistics, ImportSessionStatus status);

    Task NotifyStartedAsync(string sessionId, string fileName);

    Task NotifyCompletedAsync(string sessionId, ImportStatistics statistics);

    Task NotifyFailedAsync(string sessionId, string errorMessage);
}

/// <summary>
/// SignalR-based implementation of IImportProgressService.
/// Broadcasts import progress to all connected clients in the "import" group.
/// </summary>
public sealed class ImportProgressService : IImportProgressService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ImportProgressService> _logger;

    public ImportProgressService(IHubContext<NotificationHub> hubContext, ILogger<ImportProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(string sessionId, ImportStatistics statistics, ImportSessionStatus status)
    {
        _logger.LogDebug("Import progress: {SessionId} — {Progress}% — {Status}",
            sessionId, statistics.ProgressPercentage, status);

        await _hubContext.Clients.All.SendAsync("ImportProgress", new
        {
            SessionId = sessionId,
            Status = status.ToString(),
            statistics.TotalRecords,
            statistics.ImportedRecords,
            statistics.SkippedRecords,
            statistics.FailedRecords,
            statistics.DuplicateRecords,
            statistics.ProcessedRecords,
            statistics.ProgressPercentage
        });
    }

    public async Task NotifyStartedAsync(string sessionId, string fileName)
    {
        _logger.LogInformation("Import started: {SessionId} — {FileName}", sessionId, fileName);
        await _hubContext.Clients.All.SendAsync("ImportStarted", new { SessionId = sessionId, FileName = fileName });
    }

    public async Task NotifyCompletedAsync(string sessionId, ImportStatistics statistics)
    {
        _logger.LogInformation("Import completed: {SessionId} — {Imported}/{Total} records",
            sessionId, statistics.ImportedRecords, statistics.TotalRecords);
        await _hubContext.Clients.All.SendAsync("ImportCompleted", new
        {
            SessionId = sessionId,
            statistics.TotalRecords,
            statistics.ImportedRecords,
            statistics.SkippedRecords,
            statistics.FailedRecords,
            statistics.DuplicateRecords
        });
    }

    public async Task NotifyFailedAsync(string sessionId, string errorMessage)
    {
        _logger.LogError("Import failed: {SessionId} — {Error}", sessionId, errorMessage);
        await _hubContext.Clients.All.SendAsync("ImportFailed", new { SessionId = sessionId, Error = errorMessage });
    }
}
