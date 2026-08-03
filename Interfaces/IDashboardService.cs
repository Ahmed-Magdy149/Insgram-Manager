using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Provides aggregated dashboard statistics, relationship breakdowns,
/// and import history for the analytics dashboard.
/// All methods are asynchronous and read-only.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Returns high-level aggregate statistics for the dashboard.
    /// </summary>
    Task<DashboardStatsDto> GetStatsAsync();

    /// <summary>
    /// Returns per-relationship-type counts for the breakdown chart.
    /// </summary>
    Task<IReadOnlyList<RelationshipBreakdownItem>> GetRelationshipBreakdownAsync();

    /// <summary>
    /// Returns recent import history records, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<ImportHistoryItem>> GetImportHistoryAsync(int maxRecords = 10);

    /// <summary>
    /// Returns the most recent import record, or null if no imports exist.
    /// </summary>
    Task<ImportHistoryItem?> GetLastImportAsync();
}

/// <summary>
/// Aggregate statistics for the dashboard.
/// </summary>
public sealed record DashboardStatsDto
{
    public int ImportedAccountCount { get; init; }
    public int VerifiedAccountCount { get; init; }
    public int PrivateAccountCount { get; init; }
    public long TotalFollowers { get; init; }
    public long TotalFollowing { get; init; }
    public int TotalRelationships { get; init; }
    public int FollowersCount { get; init; }
    public int FollowingCount { get; init; }
    public int PendingRequestsCount { get; init; }
    public int BlockedCount { get; init; }
    public int RecentFollowRequestsCount { get; init; }
    public int RemovedSuggestionsCount { get; init; }
    public int TotalImports { get; init; }
}

/// <summary>
/// A single entry in the relationship breakdown chart.
/// </summary>
public sealed record RelationshipBreakdownItem
{
    public RelationshipType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public string Color { get; init; } = "blue";
    public double Percentage { get; init; }
}

/// <summary>
/// A single import history record for the dashboard table.
/// </summary>
public sealed record ImportHistoryItem
{
    public string SessionId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalRecords { get; init; }
    public int ImportedRecords { get; init; }
    public int AccountCount { get; init; }
    public int RelationshipCount { get; init; }
    public string? ErrorMessage { get; init; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt - StartedAt : null;
}
