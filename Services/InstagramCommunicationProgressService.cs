using Insgram_Manager.Interfaces;
using Insgram_Manager.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Insgram_Manager.Services;

/// <summary>
/// SignalR broadcaster for Instagram communication updates.
/// </summary>
public sealed class InstagramCommunicationProgressService : IInstagramCommunicationProgressService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public InstagramCommunicationProgressService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyCommunicationChangedAsync(string accountKey, string reason)
    {
        return _hubContext.Clients.All.SendAsync("InstagramCommunicationChanged", new
        {
            accountKey,
            reason,
            timestamp = DateTime.UtcNow
        });
    }
}
