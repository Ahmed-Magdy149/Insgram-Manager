using Insgram_Manager.Communication.Models;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Interfaces.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Pending Requests page.
/// Displays pending follow requests, supports single/multi/all selection,
/// batch cancellation with progress, retry of failed cancellations,
/// and real-time updates via SignalR.
/// </summary>
public class PendingRequestsPageBase : AuthGuardComponentBase, IAsyncDisposable
{
    [Inject]
    private IPendingRequestService PendingRequestService { get; set; } = default!;

    [Inject]
    private IInstagramSessionService SessionService { get; set; } = default!;

    [Inject]
    private IInstagramDeleteService DeleteService { get; set; } = default!;

    protected IReadOnlyList<PendingRequestItem> PendingRequests { get; set; } = [];
    protected IReadOnlyList<InstagramSessionState> Sessions { get; set; } = [];
    protected string SelectedAccountKey { get; set; } = string.Empty;

    protected HashSet<string> SelectedUsernames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    protected bool SelectAll { get; set; }
    protected bool IsLoading { get; set; }
    protected bool IsProcessing { get; set; }
    protected bool IsLive { get; set; }

    protected PendingRequestBatchProgress? CurrentProgress { get; set; }
    protected List<PendingRequestCancelResult> LastResults { get; set; } = [];
    protected List<PendingRequestCancelResult> FailedResults => LastResults.Where(r => !r.Success).ToList();

    private HubConnection? _hubConnection;
    private CancellationTokenSource? _cts;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await SetupSignalRConnectionAsync();
        await TryRestorePendingActionAsync();
    }

    private async Task TryRestorePendingActionAsync()
    {
        var action = TryRestorePendingAction();
        if (action is null)
            return;

        LogService.LogInformation("Restoring pending action {ActionName} on Pending Requests", action.ActionName);

        if (action.SelectedUsernames.Count > 0)
        {
            SelectedUsernames = action.SelectedUsernames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        StateHasChanged();

        if (action.ActionName == "CancelSelected" && SelectedUsernames.Count > 0)
        {
            await CancelSelectedAsync();
        }
        else if (action.ActionName == "CancelAll")
        {
            await CancelAllAsync();
        }
        else if (action.ActionName == "CancelSingle" && action.SelectedUsernames.Count > 0)
        {
            await CancelSingleAsync(action.SelectedUsernames.First());
        }
    }

    protected async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var pendingTask = PendingRequestService.GetPendingRequestsAsync();
            var sessionsTask = SessionService.GetSessionsAsync();

            await Task.WhenAll(pendingTask, sessionsTask);

            PendingRequests = pendingTask.Result;
            Sessions = sessionsTask.Result;

            if (string.IsNullOrWhiteSpace(SelectedAccountKey) && Sessions.Count > 0)
                SelectedAccountKey = Sessions.First().AccountKey;

            SelectedUsernames.RemoveWhere(u => !PendingRequests.Any(p => p.Username.Equals(u, StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to load pending requests.", ex);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected void ToggleSelection(string username)
    {
        if (!SelectedUsernames.Remove(username))
            SelectedUsernames.Add(username);

        SelectAll = PendingRequests.Count > 0 && SelectedUsernames.Count == PendingRequests.Count;
    }

    protected void ToggleSelectAll()
    {
        if (SelectAll)
        {
            SelectedUsernames.Clear();
            SelectAll = false;
        }
        else
        {
            SelectedUsernames = PendingRequests.Select(p => p.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
            SelectAll = true;
        }
    }

    protected async Task CancelSelectedAsync()
    {
        if (SelectedUsernames.Count == 0)
        {
            MessageService.Warning("Please select at least one pending request to cancel.");
            return;
        }

        var canProceed = await RequireInstagramLoginAsync(
            "CancelSelected",
            null,
            SelectedUsernames.ToList());

        if (!canProceed)
            return;

        await ProcessCancellationAsync(SelectedUsernames.ToList());
    }

    protected async Task CancelSingleAsync(string username)
    {
        var canProceed = await RequireInstagramLoginAsync(
            "CancelSingle",
            null,
            [username]);

        if (!canProceed)
            return;

        IsProcessing = true;
        try
        {
            var result = await DeleteService.DeleteSingleAsync(username);

            if (result.Status == DeleteItemStatus.Deleted)
                await NotifySuccessAsync($"Successfully cancelled pending request for {username}.");
            else
                await HandleErrorAsync($"Failed to cancel request for {username}: {result.Error ?? result.Status.ToString()}");

            await LoadAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync($"Error cancelling request for {username}.", ex);
        }
        finally
        {
            IsProcessing = false;
            StateHasChanged();
        }
    }

    protected async Task CancelAllAsync()
    {
        if (PendingRequests.Count == 0)
        {
            MessageService.Warning("No pending requests to cancel.");
            return;
        }

        var canProceed = await RequireInstagramLoginAsync(
            "CancelAll",
            null,
            PendingRequests.Select(p => p.Username).ToList());

        if (!canProceed)
            return;

        await ProcessCancellationAsync(PendingRequests.Select(p => p.Username).ToList());
    }

    protected async Task RetryFailedAsync()
    {
        if (FailedResults.Count == 0)
        {
            MessageService.Info("No failed cancellations to retry.");
            return;
        }

        var failedUsernames = FailedResults.Select(r => r.Username).ToList();
        await ProcessCancellationAsync(failedUsernames, isRetry: true);
    }

    private async Task ProcessCancellationAsync(List<string> usernames, bool isRetry = false)
    {
        IsProcessing = true;
        LastResults = [];
        CurrentProgress = new PendingRequestBatchProgress { Total = usernames.Count, Processed = 0, Succeeded = 0, Failed = 0 };
        _cts = new CancellationTokenSource();

        try
        {
            var accountKey = await AppSession.GetCurrentAccountKeyAsync(_cts.Token);
            if (accountKey is null)
            {
                await HandleErrorAsync("No active Instagram session.");
                return;
            }

            var progress = new Progress<PendingRequestBatchProgress>(p =>
            {
                CurrentProgress = p;
                StateHasChanged();
            });

            var results = isRetry
                ? await PendingRequestService.RetryFailedAsync(LastResults, accountKey, progress, _cts.Token)
                : await PendingRequestService.CancelBatchAsync(usernames, accountKey, progress, _cts.Token);

            LastResults = results.ToList();

            var succeeded = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);

            if (failed == 0)
                await NotifySuccessAsync($"Successfully cancelled {succeeded} pending request(s).");
            else
                await HandleErrorAsync($"Completed with {succeeded} succeeded and {failed} failed cancellation(s).");

            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            MessageService.Info("Cancellation was stopped.");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("An error occurred during batch cancellation.", ex);
        }
        finally
        {
            IsProcessing = false;
            CurrentProgress = null;
            _cts?.Dispose();
            _cts = null;
            StateHasChanged();
        }
    }

    protected void StopProcessing()
    {
        _cts?.Cancel();
    }

    protected string GetStatusColor(bool success) => success ? "green" : "red";

    private async Task SetupSignalRConnectionAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/hubs/notifications"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<PendingRequestBatchProgress>("PendingRequestProgress", progress =>
        {
            InvokeAsync(() =>
            {
                IsLive = true;
                CurrentProgress = progress;
                StateHasChanged();
            });
            return Task.CompletedTask;
        });

        _hubConnection.On<object>("PendingRequestCompleted", async _ =>
        {
            await InvokeAsync(async () =>
            {
                IsLive = true;
                await LoadAsync();
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
            LogService.LogWarning("Failed to connect to SignalR hub for pending requests: {Message}", ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
