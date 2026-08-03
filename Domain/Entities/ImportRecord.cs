namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents a completed import operation stored in the database.
/// Tracks session metadata, record counts, and timing for dashboard history and analytics.
/// </summary>
public class ImportRecord : BaseEntity
{
    public string SessionId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string Status { get; set; } = "Completed";

    public int TotalRecords { get; set; }

    public int ImportedRecords { get; set; }

    public int SkippedRecords { get; set; }

    public int FailedRecords { get; set; }

    public int AccountCount { get; set; }

    public int RelationshipCount { get; set; }

    public string? ErrorMessage { get; set; }
}
