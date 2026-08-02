namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents the result of an import operation.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ImportResult
{
    public bool Success { get; init; }

    public int TotalRecords { get; init; }

    public int ImportedRecords { get; init; }

    public int SkippedRecords { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public string? Message { get; init; }
}
