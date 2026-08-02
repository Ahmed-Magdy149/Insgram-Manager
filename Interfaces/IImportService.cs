using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for the import service that coordinates parsing
/// and persistence of imported data (e.g. Instagram data exports).
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IImportService
{
    Task<ImportResult> ImportAsync(ImportRequest request);
}
