using HtmlAgilityPack;

namespace Insgram_Manager.Infrastructure;

/// <summary>
/// Generic HTML parsing abstraction for loading and querying HTML documents.
/// Not specific to Instagram — provides a testable wrapper around HtmlAgilityPack.
/// </summary>
public interface IHtmlParser
{
    HtmlDocument LoadHtml(string html);

    HtmlDocument LoadFromFile(string path);

    HtmlDocument LoadFromUri(string uri);
}
