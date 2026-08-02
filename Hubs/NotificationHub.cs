using Microsoft.AspNetCore.SignalR;

namespace Insgram_Manager.Hubs;

/// <summary>
/// SignalR hub for real-time notifications.
/// Will be used in Phase 2 to broadcast Instagram automation events
/// (DM sent, follow/unfollow, post scheduled, etc.) to connected clients.
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Adds the current connection to a named notification group.
    /// </summary>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Removes the current connection from a named notification group.
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}
