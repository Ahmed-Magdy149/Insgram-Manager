namespace Insgram_Manager.Search.Models;

/// <summary>
/// Represents the result of a search operation.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class SearchResult
{
    public IReadOnlyList<SearchResultItem> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
