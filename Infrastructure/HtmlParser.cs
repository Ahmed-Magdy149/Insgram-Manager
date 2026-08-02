using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Infrastructure;

/// <summary>
/// HtmlAgilityPack-based implementation of IHtmlParser.
/// Provides generic HTML loading from string, file, and URI.
/// No Instagram-specific parsing logic.
/// </summary>
public sealed class HtmlParser : IHtmlParser
{
    private readonly ILogger<HtmlParser> _logger;
    private readonly HtmlDocument _document;

    public HtmlParser(ILogger<HtmlParser> logger)
    {
        _logger = logger;
        _document = new HtmlDocument();
    }

    public HtmlDocument LoadHtml(string html)
    {
        _logger.LogDebug("Loading HTML from string (length: {Length})", html.Length);
        _document.LoadHtml(html);
        return _document;
    }

    public HtmlDocument LoadFromFile(string path)
    {
        _logger.LogDebug("Loading HTML from file: {Path}", path);
        _document.Load(path);
        return _document;
    }

    public HtmlDocument LoadFromUri(string uri)
    {
        _logger.LogDebug("Loading HTML from URI: {Uri}", uri);
        var web = new HtmlWeb();
        return web.Load(uri);
    }
}
