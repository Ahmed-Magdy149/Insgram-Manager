using Insgram_Manager.Search.Models;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for searching across entities in the application.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface ISearchService
{
    Task<SearchResult> SearchAsync(SearchRequest request);
}
