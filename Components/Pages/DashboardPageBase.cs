using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Dashboard page.
/// Loads aggregated statistics via IDashboardService and listens for
/// real-time import completion events via SignalR to auto-refresh.
/// </summary>
public class DashboardPageBase : BaseComponent, IAsyncDisposable
{
    [Inject]
    private IDashboardService DashboardService { get; set; } = default!;

    [Inject]
    private IDbContextFactory<Data.ApplicationDbContext> DbContextFactory { get; set; } = default!;

    protected DashboardStatsDto Stats { get; set; } = new();
    protected IReadOnlyList<RelationshipBreakdownItem> Breakdown { get; set; } = [];
    protected IReadOnlyList<ImportHistoryItem> ImportHistory { get; set; } = [];
    protected ImportHistoryItem? LastImport { get; set; }
    protected IReadOnlyList<InstagramAccount> RecentAccounts { get; set; } = [];

    protected bool IsRefreshing { get; set; }
    protected bool IsLive { get; set; }

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardAsync();
        await SetupSignalRConnectionAsync();
    }

    protected async Task LoadDashboardAsync()
    {
        IsRefreshing = true;
        try
        {
            var statsTask = DashboardService.GetStatsAsync();
            var breakdownTask = DashboardService.GetRelationshipBreakdownAsync();
            var historyTask = DashboardService.GetImportHistoryAsync(10);
            var lastImportTask = DashboardService.GetLastImportAsync();

            await Task.WhenAll(statsTask, breakdownTask, historyTask, lastImportTask);

            Stats = statsTask.Result;
            Breakdown = breakdownTask.Result;
            ImportHistory = historyTask.Result;
            LastImport = lastImportTask.Result;

            await using var context = await DbContextFactory.CreateDbContextAsync();
            RecentAccounts = await context.InstagramAccounts
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to load dashboard data.", ex);
        }
        finally
        {
            IsRefreshing = false;
            StateHasChanged();
        }
    }

    protected async Task RefreshAsync()
    {
        await LoadDashboardAsync();
        await NotifySuccessAsync("Dashboard refreshed.");
    }

    protected void NavigateToImport()
    {
        NavigationManager.NavigateTo("/import");
    }

    protected void NavigateToDataExplorer()
    {
        NavigationManager.NavigateTo("/data-explorer");
    }

    protected string GetStatusColor(string status)
    {
        return status switch
        {
            "Completed" => "green",
            "CompletedWithErrors" => "orange",
            "Failed" => "red",
            _ => "default"
        };
    }

    protected string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "—";

        return duration.Value.TotalSeconds < 1
            ? "<1s"
            : duration.Value.TotalMinutes < 1
                ? $"{duration.Value.Seconds}s"
                : $"{(int)duration.Value.TotalMinutes}m {duration.Value.Seconds}s";
    }

    private async Task SetupSignalRConnectionAsync()
    {
        var hubUrl = NavigationManager.ToAbsoluteUri("/hubs/notifications");

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("ImportCompleted", async (_) =>
        {
            await InvokeAsync(async () =>
            {
                IsLive = true;
                await LoadDashboardAsync();
                IsLive = false;
                StateHasChanged();
            });
        });

        _hubConnection.On<object>("ImportFailed", async (_) =>
        {
            await InvokeAsync(async () =>
            {
                IsLive = true;
                await LoadDashboardAsync();
                IsLive = false;
                StateHasChanged();
            });
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            LogService.LogWarning("Failed to connect to SignalR hub for dashboard: {Message}", ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
