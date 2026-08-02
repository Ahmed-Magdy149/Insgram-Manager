namespace Insgram_Manager.Export.Models;

/// <summary>
/// Represents an export request with format and data selection.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class ExportRequest
{
    public ExportFormat Format { get; set; } = ExportFormat.Csv;

    public string FileName { get; set; } = string.Empty;

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Data { get; set; } = [];

    public IReadOnlyList<string> Columns { get; set; } = [];
}

/// <summary>
/// Supported export formats.
/// </summary>
public enum ExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}
