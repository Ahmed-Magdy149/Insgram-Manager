namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents an import request with source file and format metadata.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ImportRequest
{
    public string FileName { get; set; } = string.Empty;

    public Stream? FileStream { get; set; }

    public ImportFormat Format { get; set; } = ImportFormat.Json;
}

/// <summary>
/// Supported import formats.
/// </summary>
public enum ImportFormat
{
    Json = 0,
    Csv = 1,
    Html = 2
}
