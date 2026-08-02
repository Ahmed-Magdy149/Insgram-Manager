namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents a single import session with lifecycle tracking.
/// Created when an import operation starts and updated throughout its execution.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ImportSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string FileName { get; set; } = string.Empty;

    public ImportFormat Format { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public ImportSessionStatus Status { get; set; } = ImportSessionStatus.Pending;

    public ImportStatistics Statistics { get; set; } = new();

    public IReadOnlyList<ImportFileInfo> Files { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents the lifecycle status of an import session.
/// </summary>
public enum ImportSessionStatus
{
    Pending = 0,
    Extracting = 1,
    Parsing = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}
