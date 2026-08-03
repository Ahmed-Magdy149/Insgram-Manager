namespace Insgram_Manager.Interfaces;

using Insgram_Manager.Communication.Models;

/// <summary>
/// Abstraction for managing Instagram session state (login, cookies, authentication).
/// Used by the Instagram communication engine to persist local session state.
/// </summary>
public interface IInstagramSessionService
{
    Task<bool> IsAuthenticatedAsync(string accountKey);

    Task LoginAsync(string username, string password);

    Task LogoutAsync(string accountKey);

    Task<string?> GetSessionTokenAsync(string accountKey);

    Task RefreshSessionAsync(string accountKey);

    Task<InstagramSessionState?> GetSessionAsync(string accountKey);

    Task<IReadOnlyList<InstagramSessionState>> GetSessionsAsync();

    Task<IReadOnlyList<InstagramInteractionRecord>> GetInteractionHistoryAsync(int maxRecords = 50);

    Task<InstagramCommunicationStatsDto> GetStatsAsync();

    Task<bool> UpsertSessionAsync(InstagramSessionState session);

    Task<bool> RecordInteractionAsync(InstagramInteractionRecord record);

    Task<bool> StoreSessionWithCookiesAsync(string username, string cookieHeader, string? csrfToken = null, string? accessToken = null);

    Task<bool> ValidateSessionAsync(string accountKey);
}
