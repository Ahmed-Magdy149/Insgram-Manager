using Insgram_Manager.Export.Models;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for exporting data in multiple formats.
/// Implementations will support CSV, Excel, and PDF in Phase 2.
/// </summary>
public interface IExportService
{
    Task<ExportResult> ExportAsync(ExportRequest request);
}
