using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;
using Insgram_Manager.Import.Parsers;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Import.Factories;

/// <summary>
/// Factory implementation for resolving the correct parser based on import format.
/// Returns the appropriate IInstagramImportParser for JSON, CSV, or HTML formats.
/// </summary>
public sealed class ImportParserFactory : IImportParserFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ImportParserFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IInstagramImportParser? GetParser(ImportFormat format)
    {
        return format switch
        {
            ImportFormat.Json => new JsonImportParser(),
            ImportFormat.Csv => new CsvImportParser(),
            ImportFormat.Html => new HtmlImportParser(_loggerFactory.CreateLogger<HtmlImportParser>()),
            _ => null
        };
    }
}
