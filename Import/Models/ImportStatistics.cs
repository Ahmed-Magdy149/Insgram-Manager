namespace Insgram_Manager.Import.Models;

/// <summary>
/// Tracks statistics during an import operation.
/// Updated as records are parsed, validated, and persisted.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ImportStatistics
{
    public int TotalRecords { get; set; }

    public int ImportedRecords { get; set; }

    public int SkippedRecords { get; set; }

    public int FailedRecords { get; set; }

    public int DuplicateRecords { get; set; }

    public int ProcessedRecords => ImportedRecords + SkippedRecords + FailedRecords;

    public double ProgressPercentage =>
        TotalRecords == 0 ? 0 : Math.Round((double)ProcessedRecords / TotalRecords * 100, 2);
}
