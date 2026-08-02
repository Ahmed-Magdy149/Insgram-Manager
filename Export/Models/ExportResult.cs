namespace Insgram_Manager.Export.Models;

/// <summary>
/// Represents the result of an export operation.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ExportResult
{
    public bool Success { get; init; }

    public string? FilePath { get; init; }

    public byte[]? FileBytes { get; init; }

    public string? ContentType { get; init; }

    public string? Error { get; init; }
}
