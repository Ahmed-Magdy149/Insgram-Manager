namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents a Playwright-based Instagram authentication session with a persistent browser profile.
/// Stores metadata about the browser profile, login status, and profile information.
/// The actual cookies, local storage, and session storage are persisted by Playwright in the profile directory.
/// </summary>
public sealed class InstagramAuthSession : BaseEntity
{
    /// <summary>
    /// Unique key identifying this auth session, derived from the Instagram username (lowercased).
    /// </summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>
    /// Instagram username associated with this session.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown on the Instagram profile.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// URL of the profile picture from Instagram.
    /// </summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// Absolute path to the persistent Playwright browser profile directory.
    /// </summary>
    public string ProfileDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user is currently logged in to Instagram via this browser profile.
    /// </summary>
    public bool IsLoggedIn { get; set; }

    /// <summary>
    /// Timestamp of the last successful login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Timestamp of the last session validation check.
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// Whether the user chose to persist this session across application restarts.
    /// </summary>
    public bool RememberSession { get; set; }
}
