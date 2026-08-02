namespace Insgram_Manager.Search.Models;

/// <summary>
/// Represents a search request with query, filtering, and pagination.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class SearchRequest
{
    public string Query { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; }

    public bool SortDescending { get; set; }
}
