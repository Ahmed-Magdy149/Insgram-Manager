using System.Text;
using System.Threading;
using Insgram_Manager.Communication.Models;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Services;
using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Data Explorer page.
/// Provides browsing, search, sorting, pagination, column selection, and CSV export for imported relationship data.
/// </summary>
public class DataExplorerPageBase : AuthGuardComponentBase
{
    private static readonly TimeSpan SessionStatusRefreshInterval = TimeSpan.FromSeconds(20);

    [Inject]
    private IDbContextFactory<Data.ApplicationDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private IInstagramDeleteService DeleteService { get; set; } = default!;

    protected List<InstagramRelationship> FilteredItems { get; set; } = [];

    protected string SearchTerm { get; set; } = string.Empty;
    protected RelationshipType SelectedType { get; set; } = RelationshipType.Followers;
    protected List<InstagramRelationship> SelectedRows { get; set; } = [];

    protected int PageIndex { get; set; } = 1;
    protected int PageSize { get; set; } = 10;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; }
    protected long? DeletingRowId { get; set; }
    protected bool IsDeletingSelectedRows { get; set; }
    protected bool IsDeletingAllRows { get; set; }
    protected bool IsDeleteInProgress { get; set; }
    protected DeleteProgress DeleteProgress { get; set; } = new();

    protected bool ShowUsername { get; set; } = true;
    protected bool ShowFullName { get; set; } = true;
    protected bool ShowProfileUrl { get; set; } = true;
    protected bool ShowType { get; set; } = false;
    protected bool ShowImportedAt { get; set; } = true;

    protected bool IsRefreshingSessionStatus { get; set; }
    protected bool GlobalSessionIsConnected { get; set; }
    protected string? GlobalSessionUsername { get; set; }
    protected DateTime? GlobalSessionLastValidation { get; set; }

    private string? _lastSearchTerm;
    private CancellationTokenSource? _sessionStatusMonitoringCts;
    private Task? _sessionStatusMonitoringTask;
    private CancellationTokenSource? _deleteCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadGlobalSessionStatusAsync();
        await LoadDataAsync();
        StartSessionStatusMonitoring();
        await TryRestorePendingActionAsync();
    }

    private async Task TryRestorePendingActionAsync()
    {
        var action = TryRestorePendingAction();
        if (action is null)
            return;

        LogService.LogInformation("Restoring pending action {ActionName} on Data Explorer", action.ActionName);

        if (action.SelectedIds.Count > 0)
        {
            var idSet = action.SelectedIds.ToHashSet();
            SelectedRows = FilteredItems.Where(r => idSet.Contains(r.Id)).ToList();
        }

        if (action.SelectedUsernames.Count > 0 && SelectedType != RelationshipType.PendingRequests)
        {
            SelectedType = RelationshipType.PendingRequests;
            await LoadDataAsync();
        }

        if (action.SelectedUsernames.Count > 0)
        {
            var usernameSet = action.SelectedUsernames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            SelectedRows = FilteredItems.Where(r => usernameSet.Contains(r.Username)).ToList();
        }

        StateHasChanged();

        if (action.ActionName == "DeleteSelected" && SelectedRows.Count > 0)
        {
            await DeleteSelectedAsync();
        }
        else if (action.ActionName == "DeleteSingle" && SelectedRows.Count > 0)
        {
            await DeleteRowAsync(SelectedRows.First());
        }
    }

    protected bool IsInstagramSideActionSupported => SelectedType == RelationshipType.PendingRequests;

    protected string InstagramActionLabel => SelectedType == RelationshipType.PendingRequests
        ? "Cancel selected on Instagram"
        : "Instagram action unavailable for this type";

    private async Task LoadGlobalSessionStatusAsync()
    {
        try
        {
            GlobalSessionIsConnected = await AppSession.IsLoggedInAsync();
            GlobalSessionUsername = GlobalSessionIsConnected
                ? await AppSession.GetCurrentUsernameAsync()
                : null;
            GlobalSessionLastValidation = await AppSession.GetLastValidationAsync();
        }
        catch (Exception ex)
        {
            LogService.LogWarning("Failed to load global session status: {Message}", ex.Message);
            GlobalSessionIsConnected = false;
            GlobalSessionUsername = null;
        }
    }

    private void StartSessionStatusMonitoring()
    {
        _sessionStatusMonitoringCts?.Cancel();
        _sessionStatusMonitoringCts?.Dispose();

        _sessionStatusMonitoringCts = new CancellationTokenSource();
        _sessionStatusMonitoringTask = MonitorSessionStatusAsync(_sessionStatusMonitoringCts.Token);
    }

    private async Task MonitorSessionStatusAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SessionStatusRefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await LoadGlobalSessionStatusAsync();
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    protected async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync();

            var query = context.InstagramRelationships
                .AsNoTracking()
                .Where(r => r.RelationshipType == SelectedType);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                query = query.Where(r =>
                    r.Username.ToLower().Contains(term) ||
                    (r.FullName != null && r.FullName.ToLower().Contains(term)));
            }

            TotalCount = await query.CountAsync();

            FilteredItems = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionStatusMonitoringCts?.Cancel();
            _sessionStatusMonitoringCts?.Dispose();
            _deleteCts?.Cancel();
            _deleteCts?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected async Task OnSearch()
    {
        if (_lastSearchTerm == SearchTerm)
            return;

        _lastSearchTerm = SearchTerm;
        PageIndex = 1;
        await LoadDataAsync();
        StateHasChanged();
    }

    protected async Task OnTypeTabChanged(string key)
    {
        if (int.TryParse(key, out var value) && Enum.IsDefined(typeof(RelationshipType), value))
        {
            SelectedType = (RelationshipType)value;
            PageIndex = 1;
            await LoadDataAsync();
            StateHasChanged();
        }
    }

    protected async Task OnTableChange(AntDesign.TableModels.QueryModel<InstagramRelationship> query)
    {
        PageIndex = query.PageIndex;
        PageSize = query.PageSize;
        await LoadDataAsync();
    }

    protected void OnSelectedRowsChanged(IEnumerable<InstagramRelationship> selectedRows)
    {
        SelectedRows = selectedRows.DistinctBy(r => r.Id).ToList();
    }

    protected async Task DeleteSelectedAsync()
    {
        if (SelectedRows.Count == 0)
        {
            MessageService.Warning("Select one or more rows first.");
            return;
        }

        if (!IsInstagramSideActionSupported)
        {
            MessageService.Warning("Instagram-side cancellation is only supported for Pending Requests.");
            return;
        }

        var usernames = SelectedRows
            .Select(r => r.Username)
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (usernames.Count == 0)
        {
            MessageService.Info("No selected usernames were found.");
            return;
        }

        var selectedIds = SelectedRows.Select(r => (long)r.Id).ToList();

        var canProceed = await RequireInstagramLoginAsync(
            "DeleteSelected",
            selectedIds,
            usernames);

        if (!canProceed)
            return;

        IsDeletingSelectedRows = true;
        IsDeleteInProgress = true;
        _deleteCts = new CancellationTokenSource();
        DeleteProgress = new DeleteProgress { Total = usernames.Count };

        try
        {
            var progress = new Progress<DeleteProgress>(p =>
            {
                DeleteProgress = p;
                StateHasChanged();
            });

            var result = await DeleteService.DeleteSelectedAsync(usernames, progress, _deleteCts.Token);

            SelectedRows.Clear();
            await LoadDataAsync();
            await LoadGlobalSessionStatusAsync();

            if (result.Success)
            {
                MessageService.Success($"Deleted {result.Succeeded} pending request(s) on Instagram.");
            }
            else if (result.Succeeded > 0)
            {
                MessageService.Warning($"Deleted {result.Succeeded} of {result.TotalRequested} — {result.Failed} failed, {result.Skipped} skipped.");
            }
            else
            {
                MessageService.Error(result.Error ?? "Failed to delete the selected requests on Instagram.");
            }
        }
        catch (OperationCanceledException)
        {
            MessageService.Info("Delete operation was cancelled.");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to delete the selected requests on Instagram.", ex);
        }
        finally
        {
            IsDeletingSelectedRows = false;
            IsDeleteInProgress = false;
            _deleteCts?.Dispose();
            _deleteCts = null;
        }
    }

    protected async Task DeleteAllAsync()
    {
        if (!IsInstagramSideActionSupported)
        {
            MessageService.Warning("Instagram-side cancellation is only supported for Pending Requests.");
            return;
        }

        var canProceed = await RequireInstagramLoginAsync("DeleteAll");

        if (!canProceed)
            return;

        IsDeletingAllRows = true;
        IsDeleteInProgress = true;
        _deleteCts = new CancellationTokenSource();
        DeleteProgress = new DeleteProgress();

        try
        {
            var progress = new Progress<DeleteProgress>(p =>
            {
                DeleteProgress = p;
                StateHasChanged();
            });

            var result = await DeleteService.DeleteAllAsync(progress, _deleteCts.Token);

            await LoadDataAsync();
            await LoadGlobalSessionStatusAsync();

            if (result.Success)
            {
                MessageService.Success($"Deleted {result.Succeeded} pending request(s) on Instagram.");
            }
            else if (result.Succeeded > 0)
            {
                MessageService.Warning($"Deleted {result.Succeeded} of {result.TotalRequested} — {result.Failed} failed, {result.Skipped} skipped.");
            }
            else if (result.TotalRequested == 0)
            {
                MessageService.Info("No pending requests to delete.");
            }
            else
            {
                MessageService.Error(result.Error ?? "Failed to delete pending requests on Instagram.");
            }
        }
        catch (OperationCanceledException)
        {
            MessageService.Info("Delete operation was cancelled.");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to delete pending requests on Instagram.", ex);
        }
        finally
        {
            IsDeletingAllRows = false;
            IsDeleteInProgress = false;
            _deleteCts?.Dispose();
            _deleteCts = null;
        }
    }

    protected void CancelDeleteProgress()
    {
        _deleteCts?.Cancel();
    }

    protected async Task DeleteRowAsync(InstagramRelationship item)
    {
        if (!IsInstagramSideActionSupported)
        {
            MessageService.Warning("Instagram-side cancellation is only supported for Pending Requests.");
            return;
        }

        var canProceed = await RequireInstagramLoginAsync(
            "DeleteSingle",
            [(long)item.Id],
            [item.Username]);

        if (!canProceed)
            return;

        DeletingRowId = item.Id;
        try
        {
            var result = await DeleteService.DeleteSingleAsync(item.Username);

            if (result.Status == DeleteItemStatus.Deleted)
            {
                SelectedRows = SelectedRows.Where(r => r.Id != item.Id).ToList();
                await LoadDataAsync();
                MessageService.Success($"Deleted '{item.Username}' on Instagram.");
            }
            else
            {
                var statusText = result.Status switch
                {
                    DeleteItemStatus.AlreadyRemoved => "Already removed",
                    DeleteItemStatus.NotFound => "Not found",
                    DeleteItemStatus.Private => "Private account",
                    DeleteItemStatus.Skipped => "Skipped",
                    _ => "Failed"
                };
                MessageService.Warning($"{statusText} for '{item.Username}'{(result.Error is not null ? $": {result.Error}" : "")}.");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync($"Failed to delete '{item.Username}' on Instagram.", ex);
        }
        finally
        {
            DeletingRowId = null;
        }
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
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var query = context.InstagramRelationships
            .AsNoTracking()
            .Where(r => r.RelationshipType == SelectedType);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Username.ToLower().Contains(term) ||
                (r.FullName != null && r.FullName.ToLower().Contains(term)));
        }

        var exportItems = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();
        var headers = new List<string>();

        if (ShowUsername) headers.Add("Username");
        if (ShowFullName) headers.Add("Full Name");
        if (ShowProfileUrl) headers.Add("Profile URL");
        if (ShowType) headers.Add("Type");
        if (ShowImportedAt) headers.Add("Imported At");

        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var item in exportItems)
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
}
