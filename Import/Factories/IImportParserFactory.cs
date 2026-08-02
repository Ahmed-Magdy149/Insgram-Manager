using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Import.Factories;

/// <summary>
/// Factory abstraction for resolving the correct parser based on import format.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IImportParserFactory
{
    IInstagramImportParser? GetParser(ImportFormat format);
}
