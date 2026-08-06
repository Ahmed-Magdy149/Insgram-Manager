namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Manages persistent Playwright browser profile directories on disk.
/// Each Instagram account gets an isolated profile directory that stores
/// cookies, local storage, session storage, and browser state.
/// </summary>
public interface IBrowserProfileService
{
    /// <summary>
    /// Returns the absolute path to the profile directory for the given account key.
    /// Creates the directory if it does not exist.
    /// </summary>
    string GetOrCreateProfileDirectory(string accountKey);

    /// <summary>
    /// Returns the absolute path to the profile directory for the given account key,
    /// or null if no profile directory exists on disk.
    /// </summary>
    string? GetProfileDirectory(string accountKey);

    /// <summary>
    /// Returns true if a profile directory exists on disk for the given account key.
    /// </summary>
    bool ProfileExists(string accountKey);

    /// <summary>
    /// Deletes the profile directory and all its contents for the given account key.
    /// </summary>
    Task<bool> DeleteProfileAsync(string accountKey);

    /// <summary>
    /// Returns the root directory containing all browser profiles.
    /// </summary>
    string GetRootProfilesDirectory();
}

/// <summary>
/// Represents profile information extracted from an Instagram session.
/// </summary>
public sealed record InstagramProfileInfo
{
    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? ProfilePictureUrl { get; init; }

    public bool IsLoggedIn { get; init; }

    public DateTime ExtractedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the result of a login attempt.
/// </summary>
public sealed record InstagramLoginResult
{
    public bool Success { get; init; }

    public string Username { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? ProfilePictureUrl { get; init; }

    public string? Error { get; init; }

    public InstagramLoginStatus Status { get; init; }

    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the status of a login attempt.
/// </summary>
public enum InstagramLoginStatus
{
    Success = 0,
    TwoFactorRequired = 1,
    ChallengeRequired = 2,
    CheckpointRequired = 3,
    InvalidCredentials = 4,
    Timeout = 5,
    Error = 6
}
