namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Orchestrates the Instagram login flow using Playwright browser automation.
/// Handles normal login, 2FA, challenges, and checkpoints.
/// Persists session metadata to the database and manages browser profiles.
/// </summary>
public interface IInstagramLoginService
{
    /// <summary>
    /// Attempts to log in to Instagram using the provided credentials.
    /// Opens a real Chromium browser, fills the login form, and waits for the result.
    /// If 2FA or challenge is required, waits for the user to complete it in the browser window.
    /// Never bypasses Instagram security.
    /// </summary>
    Task<InstagramLoginResult> LoginAsync(string username, string password, bool rememberSession, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an existing session by opening the browser profile and checking if Instagram is still logged in.
    /// </summary>
    Task<bool> ValidateSessionAsync(string accountKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns profile information for an existing session, or null if no session exists.
    /// </summary>
    Task<InstagramProfileInfo?> GetProfileInfoAsync(string accountKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out of Instagram and removes the browser profile and session metadata.
    /// </summary>
    Task LogoutAsync(string accountKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all stored auth sessions from the database.
    /// </summary>
    Task<IReadOnlyList<InstagramAuthSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single auth session by account key, or null if not found.
    /// </summary>
    Task<InstagramAuthSessionDto?> GetSessionAsync(string accountKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO representing an Instagram auth session for UI display.
/// </summary>
public sealed record InstagramAuthSessionDto
{
    public int Id { get; init; }

    public string AccountKey { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? ProfilePictureUrl { get; init; }

    public string ProfileDirectory { get; init; } = string.Empty;

    public bool IsLoggedIn { get; init; }

    public bool RememberSession { get; init; }

    public DateTime? LastLoginAt { get; init; }

    public DateTime? LastValidatedAt { get; init; }

    public DateTime CreatedAt { get; init; }
}
