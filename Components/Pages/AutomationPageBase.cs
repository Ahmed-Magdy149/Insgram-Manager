using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Automation page.
/// Provides job creation, lifecycle management (pause, resume, retry, cancel),
/// real-time updates via SignalR, and statistics display.
/// </summary>
public class AutomationPageBase : BaseComponent, IAsyncDisposable
{
    [Inject]
    private IAutomationService AutomationService { get; set; } = default!;

    protected AutomationStatsDto Stats { get; set; } = new();
    protected IReadOnlyList<AutomationJobDto> Jobs { get; set; } = [];
    protected bool IsLoading { get; set; }
    protected bool IsLive { get; set; }

    protected AutomationType SelectedType { get; set; } = AutomationType.CancelPendingRequests;

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
        await SetupSignalRConnectionAsync();
    }

    protected async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var statsTask = AutomationService.GetStatsAsync();
            var jobsTask = AutomationService.GetAllJobsAsync();

            await Task.WhenAll(statsTask, jobsTask);

            Stats = statsTask.Result;
            Jobs = jobsTask.Result;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to load automation data.", ex);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task EnqueueJobAsync()
    {
        try
        {
            var jobId = await AutomationService.EnqueueJobAsync(SelectedType);
            await NotifySuccessAsync($"Job enqueued: {jobId[..8]}...");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to enqueue job.", ex);
        }
    }

    protected async Task PauseJobAsync(string jobId)
    {
        var success = await AutomationService.PauseJobAsync(jobId);
        if (success)
            await NotifySuccessAsync("Job paused.");
        else
            await HandleErrorAsync("Cannot pause this job.");

        await LoadDataAsync();
    }

    protected async Task ResumeJobAsync(string jobId)
    {
        var success = await AutomationService.ResumeJobAsync(jobId);
        if (success)
            await NotifySuccessAsync("Job resumed.");
        else
            await HandleErrorAsync("Cannot resume this job.");

        await LoadDataAsync();
    }

    protected async Task RetryJobAsync(string jobId)
    {
        var success = await AutomationService.RetryJobAsync(jobId);
        if (success)
            await NotifySuccessAsync("Job retrying.");
        else
            await HandleErrorAsync("Cannot retry this job.");

        await LoadDataAsync();
    }

    protected async Task CancelJobAsync(string jobId)
    {
        var success = await AutomationService.CancelJobAsync(jobId);
        if (success)
            await NotifySuccessAsync("Job cancelled.");
        else
            await HandleErrorAsync("Cannot cancel this job.");

        await LoadDataAsync();
    }

    protected string GetStatusColor(JobStatus status)
    {
        return status switch
        {
            JobStatus.Pending => "blue",
            JobStatus.Running => "processing",
            JobStatus.Completed => "green",
            JobStatus.Failed => "red",
            JobStatus.Cancelled => "default",
            JobStatus.Paused => "orange",
            JobStatus.Retrying => "purple",
            _ => "default"
        };
    }

    protected string GetStatusIcon(JobStatus status)
    {
        return status switch
        {
            JobStatus.Pending => "clock-circle",
            JobStatus.Running => "loading",
            JobStatus.Completed => "check-circle",
            JobStatus.Failed => "close-circle",
            JobStatus.Cancelled => "stop",
            JobStatus.Paused => "pause-circle",
            JobStatus.Retrying => "redo",
            _ => "info-circle"
        };
    }

    protected string GetTypeLabel(AutomationType type)
    {
        return type switch
        {
            AutomationType.DirectMessage => "Direct Message",
            AutomationType.Follow => "Follow",
            AutomationType.Unfollow => "Unfollow",
            AutomationType.Like => "Like",
            AutomationType.Comment => "Comment",
            AutomationType.PostSchedule => "Post Schedule",
            AutomationType.CancelPendingRequests => "Cancel Pending Requests",
            _ => type.ToString()
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

        async Task OnJobChanged()
        {
            await InvokeAsync(async () =>
            {
                IsLive = true;
                await LoadDataAsync();
                IsLive = false;
                StateHasChanged();
            });
        }

        _hubConnection.On<object>("AutomationJobQueued", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobStarted", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobCompleted", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobFailed", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobCancelled", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobPaused", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobResumed", _ => OnJobChanged());
        _hubConnection.On<object>("AutomationJobRetrying", _ => OnJobChanged());

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            LogService.LogWarning("Failed to connect to SignalR hub for automation: {Message}", ex.Message);
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
