using Insgram_Manager.Export.Models;

namespace Insgram_Manager.Export.Interfaces;

/// <summary>
/// Abstraction for CSV export operations.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface ICsvExportService
{
    Task<ExportResult> ExportAsync(ExportRequest request);
}
