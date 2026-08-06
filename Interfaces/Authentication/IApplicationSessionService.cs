namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Centralized session management — the single source of truth for Instagram authentication.
/// Bridges Playwright-based browser sessions with the communication session layer.
/// Every page and service queries this service to determine login state.
/// </summary>
public interface IApplicationSessionService
{
    /// <summary>
    /// Returns true if there is a valid, authenticated Instagram session.
    /// Checks Playwright auth sessions first, then falls back to communication sessions.
    /// </summary>
    Task<bool> IsLoggedInAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active account key (username lowercased) if logged in, otherwise null.
    /// </summary>
    Task<string?> GetCurrentAccountKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active username if logged in, otherwise null.
    /// </summary>
    Task<string?> GetCurrentUsernameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the current session and updates its status.
    /// Returns true if the session is still valid.
    /// </summary>
    Task<bool> ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out of the current session and removes associated data.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the last validation timestamp for the current session.
    /// </summary>
    Task<DateTime?> GetLastValidationAsync(CancellationToken cancellationToken = default);
}
