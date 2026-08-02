namespace Insgram_Manager.Search.Models;

/// <summary>
/// Represents a single item in a search result.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class SearchResultItem
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? Type { get; set; }

    public IReadOnlyDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
