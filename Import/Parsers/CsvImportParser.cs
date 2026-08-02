using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Import.Parsers;

/// <summary>
/// Parser for CSV-format Instagram data imports.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class CsvImportParser : IInstagramImportParser
{
    public bool CanParse(ImportFormat format) => format == ImportFormat.Csv;

    public Task<ImportResult> ParseAsync(ImportRequest request)
    {
        throw new NotImplementedException("Phase 2 — CSV import parsing not yet implemented.");
    }
}
