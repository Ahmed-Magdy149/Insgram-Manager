namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents metadata for a single file within an import archive or batch.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ImportFileInfo
{
    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime LastModified { get; set; }

    public ImportFormat? DetectedFormat { get; set; }

    public bool IsValid { get; set; }

    public string? ValidationMessage { get; set; }
}
