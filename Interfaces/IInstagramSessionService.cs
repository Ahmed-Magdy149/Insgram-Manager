namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for managing Instagram session state (login, cookies, authentication).
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IInstagramSessionService
{
    Task<bool> IsAuthenticatedAsync(string accountKey);

    Task LoginAsync(string username, string password);

    Task LogoutAsync(string accountKey);

    Task<string?> GetSessionTokenAsync(string accountKey);

    Task RefreshSessionAsync(string accountKey);
}
