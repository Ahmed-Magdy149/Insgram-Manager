namespace Insgram_Manager.Interfaces;

/// <summary>
/// Broadcasts Instagram communication updates to connected clients.
/// </summary>
public interface IInstagramCommunicationProgressService
{
    Task NotifyCommunicationChangedAsync(string accountKey, string reason);
}
