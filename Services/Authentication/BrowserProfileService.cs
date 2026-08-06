using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Manages persistent Playwright browser profile directories on disk.
/// Each Instagram account gets an isolated profile directory under the configured root.
/// </summary>
public sealed class BrowserProfileService : IBrowserProfileService
{
    private readonly string _rootProfilesDirectory;
    private readonly ILogger<BrowserProfileService> _logger;

    public BrowserProfileService(IOptions<BrowserAuthOptions> options, ILogger<BrowserProfileService> logger)
    {
        _logger = logger;
        var configuredPath = options.Value.ProfilesDirectory;

        _rootProfilesDirectory = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }

    public string GetOrCreateProfileDirectory(string accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
            throw new ArgumentException("Account key is required.", nameof(accountKey));

        var safeKey = SanitizeAccountKey(accountKey);
        var profilePath = Path.Combine(_rootProfilesDirectory, safeKey);

        if (!Directory.Exists(profilePath))
        {
            Directory.CreateDirectory(profilePath);
            _logger.LogInformation("Created browser profile directory: {ProfilePath}", profilePath);
        }

        return profilePath;
    }

    public string? GetProfileDirectory(string accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
            return null;

        var safeKey = SanitizeAccountKey(accountKey);
        var profilePath = Path.Combine(_rootProfilesDirectory, safeKey);

        return Directory.Exists(profilePath) ? profilePath : null;
    }

    public bool ProfileExists(string accountKey)
    {
        return GetProfileDirectory(accountKey) is not null;
    }

    public async Task<bool> DeleteProfileAsync(string accountKey)
    {
        var profilePath = GetProfileDirectory(accountKey);
        if (profilePath is null)
            return false;

        try
        {
            await Task.Run(() =>
            {
                Directory.Delete(profilePath, recursive: true);
            });

            _logger.LogInformation("Deleted browser profile directory: {ProfilePath}", profilePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to delete browser profile directory: {ProfilePath}", profilePath);
            return false;
        }
    }

    public string GetRootProfilesDirectory() => _rootProfilesDirectory;

    private static string SanitizeAccountKey(string accountKey)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder(accountKey.Length);

        foreach (var c in accountKey)
        {
            sanitized.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return sanitized.ToString().ToLowerInvariant();
    }
}
