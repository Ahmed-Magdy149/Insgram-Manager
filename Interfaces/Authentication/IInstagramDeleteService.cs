using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Result of a single delete operation on one Instagram account.
/// </summary>
public sealed record DeleteItemResult
{
    public string Username { get; init; } = string.Empty;
    public DeleteItemStatus Status { get; init; } = DeleteItemStatus.Failed;
    public string? Error { get; init; }
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Status of a single delete operation.
/// </summary>
public enum DeleteItemStatus
{
    Deleted = 0,
    Skipped = 1,
    AlreadyRemoved = 2,
    Private = 3,
    NotFound = 4,
    Failed = 5
}

/// <summary>
/// Progress update for batch delete operations.
/// </summary>
public sealed record DeleteProgress
{
    public int Total { get; init; }
    public int Processed { get; init; }
    public int Succeeded { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public string CurrentUsername { get; init; } = string.Empty;
    public double Percentage => Total == 0 ? 0 : Math.Round((double)Processed / Total * 100, 1);
}

/// <summary>
/// Result of a batch delete operation.
/// </summary>
public sealed record DeleteResult
{
    public bool Success { get; init; }
    public int TotalRequested { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<DeleteItemResult> Items { get; init; } = [];
}

/// <summary>
/// Handles Instagram-side deletion operations using the existing Playwright browser session.
/// All methods require a valid session via <see cref="IApplicationSessionService"/>.
/// Reuses the existing authenticated browser context — never creates a new login.
/// </summary>
public interface IInstagramDeleteService
{
    /// <summary>
    /// Cancels (unfollows) the selected pending follow requests on Instagram.
    /// Navigates to each account's profile and performs the unfollow action using the existing browser session.
    /// </summary>
    Task<DeleteResult> DeleteSelectedAsync(
        IReadOnlyList<string> usernames,
        IProgress<DeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels all pending follow requests on Instagram.
    /// Loads all pending requests from the database and deletes them sequentially.
    /// </summary>
    Task<DeleteResult> DeleteAllAsync(
        IProgress<DeleteProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a single pending follow request on Instagram.
    /// </summary>
    Task<DeleteItemResult> DeleteSingleAsync(
        string username,
        CancellationToken cancellationToken = default);
}
