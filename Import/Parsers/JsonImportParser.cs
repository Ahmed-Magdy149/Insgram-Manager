using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Import.Parsers;

/// <summary>
/// Parser for JSON-format Instagram data imports.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class JsonImportParser : IInstagramImportParser
{
    public bool CanParse(ImportFormat format) => format == ImportFormat.Json;

    public Task<ImportResult> ParseAsync(ImportRequest request)
    {
        throw new NotImplementedException("Phase 2 — JSON import parsing not yet implemented.");
    }
}
