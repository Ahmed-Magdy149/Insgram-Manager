using Insgram_Manager.Communication.Models;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Instagram Communication page.
/// Provides session overview, interaction history, and request testing.
/// </summary>
public class CommunicationPageBase : BaseComponent, IAsyncDisposable
{
    [Inject]
    private IInstagramSessionService SessionService { get; set; } = default!;

    [Inject]
    private IInstagramRequestService RequestService { get; set; } = default!;

    protected IReadOnlyList<InstagramSessionState> Sessions { get; set; } = [];
    protected IReadOnlyList<InstagramInteractionRecord> Interactions { get; set; } = [];
    protected InstagramCommunicationStatsDto Stats { get; set; } = new();

    protected string SelectedAccountKey { get; set; } = string.Empty;
    protected string RequestEndpoint { get; set; } = "/me";
    protected string RequestBody { get; set; } = string.Empty;
    protected string RequestMethod { get; set; } = "GET";
    protected bool IsLoading { get; set; }
    protected bool IsLive { get; set; }
    protected string? RequestResult { get; set; }

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await SetupSignalRConnectionAsync();
    }

    protected async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var sessionsTask = SessionService.GetSessionsAsync();
            var interactionsTask = SessionService.GetInteractionHistoryAsync(25);
            var statsTask = SessionService.GetStatsAsync();

            await Task.WhenAll(sessionsTask, interactionsTask, statsTask);

            Sessions = sessionsTask.Result;
            Interactions = interactionsTask.Result;
            Stats = statsTask.Result;

            SelectedAccountKey = Sessions.FirstOrDefault()?.AccountKey ?? string.Empty;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to load communication data.", ex);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    protected async Task SendTestRequestAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedAccountKey))
        {
            await HandleErrorAsync("Please choose an account session first.");
            return;
        }

        var method = string.Equals(RequestMethod, "POST", StringComparison.OrdinalIgnoreCase)
            ? HttpMethod.Post
            : HttpMethod.Get;

        HttpContent? content = null;
        if (method == HttpMethod.Post && !string.IsNullOrWhiteSpace(RequestBody))
        {
            content = new StringContent(RequestBody, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await RequestService.SendAsync(SelectedAccountKey, method, RequestEndpoint, content);
        RequestResult = response.Success
            ? $"{(int?)response.StatusCode ?? 0} - {response.Content}"
            : response.Error;

        await LoadAsync();
    }

    protected string GetStatusColor(bool authenticated) => authenticated ? "green" : "red";

    protected string FormatDateTime(DateTime? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : "—";

    protected string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "—";

        return duration.Value.TotalSeconds < 1 ? "<1s" : $"{duration.Value.TotalSeconds:F1}s";
    }

    private async Task SetupSignalRConnectionAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/hubs/notifications"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("InstagramCommunicationChanged", async _ =>
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
            LogService.LogWarning("Failed to connect to SignalR hub for communication: {Message}", ex.Message);
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
