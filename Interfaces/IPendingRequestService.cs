using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Manages the cancellation of pending Instagram follow requests.
/// Provides batch operations with progress reporting and retry support.
/// </summary>
public interface IPendingRequestService
{
    /// <summary>
    /// Retrieves all pending follow request relationships from the database.
    /// </summary>
    Task<IReadOnlyList<PendingRequestItem>> GetPendingRequestsAsync();

    /// <summary>
    /// Cancels a single pending follow request for the specified username.
    /// Uses the authenticated Instagram session to perform the unfollow action.
    /// </summary>
    Task<PendingRequestCancelResult> CancelAsync(string username, string accountKey);

    /// <summary>
    /// Cancels multiple pending follow requests in sequence with progress reporting.
    /// </summary>
    Task<IReadOnlyList<PendingRequestCancelResult>> CancelBatchAsync(
        IReadOnlyList<string> usernames,
        string accountKey,
        IProgress<PendingRequestBatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries previously failed cancellations.
    /// </summary>
    Task<IReadOnlyList<PendingRequestCancelResult>> RetryFailedAsync(
        IReadOnlyList<PendingRequestCancelResult> failedResults,
        string accountKey,
        IProgress<PendingRequestBatchProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a successfully cancelled pending request from the database.
    /// </summary>
    Task<bool> RemoveFromDatabaseAsync(string username);
}

/// <summary>
/// Represents a pending follow request item for display and selection.
/// </summary>
public sealed record PendingRequestItem
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public string? ProfileUrl { get; init; }
    public DateTime ImportedAt { get; init; }
}

/// <summary>
/// Result of a single cancel operation.
/// </summary>
public sealed record PendingRequestCancelResult
{
    public string Username { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int? StatusCode { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Progress update for batch cancellation operations.
/// </summary>
public sealed record PendingRequestBatchProgress
{
    public int Total { get; init; }
    public int Processed { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public string CurrentUsername { get; init; } = string.Empty;
    public double Percentage => Total == 0 ? 0 : Math.Round((double)Processed / Total * 100, 1);
}
