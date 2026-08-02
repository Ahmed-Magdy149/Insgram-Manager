using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Import.Interfaces;

/// <summary>
/// Abstraction for parsing Instagram data imports.
/// Each parser implementation handles a specific import format (JSON, CSV, HTML).
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IInstagramImportParser
{
    bool CanParse(ImportFormat format);

    Task<ImportResult> ParseAsync(ImportRequest request);
}
