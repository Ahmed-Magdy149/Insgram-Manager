using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Generic parser factory abstraction for resolving format-specific parsers.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IParserFactory
{
    IInstagramImportParser? GetParser(ImportFormat format);

    IReadOnlyList<ImportFormat> SupportedFormats { get; }
}
