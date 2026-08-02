using System.Text;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Data Explorer page.
/// Provides browsing, search, sorting, pagination, column selection, and CSV export for imported relationship data.
/// </summary>
public class DataExplorerPageBase : BaseComponent
{
    [Inject]
    private IDbContextFactory<Data.ApplicationDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected List<InstagramRelationship> AllItems { get; set; } = [];
    protected List<InstagramRelationship> FilteredItems { get; set; } = [];

    protected string SearchTerm { get; set; } = string.Empty;
    protected RelationshipType SelectedType { get; set; } = RelationshipType.Followers;

    protected int PageIndex { get; set; } = 1;
    protected int PageSize { get; set; } = 10;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; }

    protected bool ShowUsername { get; set; } = true;
    protected bool ShowFullName { get; set; } = true;
    protected bool ShowProfileUrl { get; set; } = true;
    protected bool ShowType { get; set; } = false;
    protected bool ShowImportedAt { get; set; } = true;

    private string? _lastSearchTerm;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    protected async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync();
            AllItems = await context.InstagramRelationships
                .AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ApplyFilterAndPaging();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void OnSearch()
    {
        if (_lastSearchTerm == SearchTerm)
            return;

        _lastSearchTerm = SearchTerm;
        PageIndex = 1;
        ApplyFilterAndPaging();
        StateHasChanged();
    }

    protected void OnTypeTabChanged(string key)
    {
        if (int.TryParse(key, out var value) && Enum.IsDefined(typeof(RelationshipType), value))
        {
            SelectedType = (RelationshipType)value;
            PageIndex = 1;
            ApplyFilterAndPaging();
            StateHasChanged();
        }
    }

    protected void OnPageChanged(int page)
    {
        PageIndex = page;
        ApplyFilterAndPaging();
    }

    protected string GetTagColor(RelationshipType type)
    {
        return type switch
        {
            RelationshipType.Followers => "blue",
            RelationshipType.Following => "green",
            RelationshipType.PendingRequests => "orange",
            RelationshipType.Blocked => "red",
            RelationshipType.RecentFollowRequests => "purple",
            RelationshipType.RemovedSuggestions => "default",
            _ => "default"
        };
    }

    protected async Task ExportCsvAsync()
    {
        var builder = new StringBuilder();
        var headers = new List<string>();

        if (ShowUsername) headers.Add("Username");
        if (ShowFullName) headers.Add("Full Name");
        if (ShowProfileUrl) headers.Add("Profile URL");
        if (ShowType) headers.Add("Type");
        if (ShowImportedAt) headers.Add("Imported At");

        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var item in FilteredItems)
        {
            var fields = new List<string>();

            if (ShowUsername) fields.Add(EscapeCsv(item.Username));
            if (ShowFullName) fields.Add(EscapeCsv(item.FullName ?? string.Empty));
            if (ShowProfileUrl) fields.Add(EscapeCsv(item.ProfileUrl ?? string.Empty));
            if (ShowType) fields.Add(EscapeCsv(item.RelationshipType.ToString()));
            if (ShowImportedAt) fields.Add(EscapeCsv(item.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

            builder.AppendLine(string.Join(",", fields));
        }

        var csv = builder.ToString();
        var fileName = $"insgram-data-explorer-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await DownloadCsvAsync(fileName, csv);
    }

    private async Task DownloadCsvAsync(string fileName, string csvContent)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csvContent)).ToArray();
        var base64 = Convert.ToBase64String(bytes);
        await JSRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, $"data:text/csv;charset=utf-8;base64,{base64}");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private void ApplyFilterAndPaging()
    {
        var query = AllItems
            .Where(r => r.RelationshipType == SelectedType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(r =>
                (r.Username != null && r.Username.ToLowerInvariant().Contains(term)) ||
                (r.FullName != null && r.FullName.ToLowerInvariant().Contains(term)));
        }

        var list = query.ToList();
        TotalCount = list.Count;

        FilteredItems = list
            .Skip((PageIndex - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
