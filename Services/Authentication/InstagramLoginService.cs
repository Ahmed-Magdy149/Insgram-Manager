using Insgram_Manager.Data;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Orchestrates the Instagram login flow using Playwright browser automation.
/// Handles normal login, 2FA, challenges, and checkpoints.
/// Persists session metadata to the database and manages browser profiles.
/// Never saves passwords. Never bypasses Instagram security.
/// </summary>
public sealed class InstagramLoginService : IInstagramLoginService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IBrowserProfileService _profileService;
    private readonly IInstagramBrowserService _browserService;
    private readonly BrowserAuthOptions _options;
    private readonly ILogger<InstagramLoginService> _logger;

    public InstagramLoginService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IBrowserProfileService profileService,
        IInstagramBrowserService browserService,
        IOptions<BrowserAuthOptions> options,
        ILogger<InstagramLoginService> logger)
    {
        _contextFactory = contextFactory;
        _profileService = profileService;
        _browserService = browserService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InstagramLoginResult> LoginAsync(string username, string password, bool rememberSession, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var accountKey = username.ToLowerInvariant();
        var profileDirectory = _profileService.GetOrCreateProfileDirectory(accountKey);
        var loginTimeout = TimeSpan.FromSeconds(_options.LoginTimeoutSeconds);

        _logger.LogInformation("Starting Instagram login for {Username} (remember={RememberSession})", username, rememberSession);

        try
        {
            var page = await _browserService.LaunchPersistentContextAsync(profileDirectory, cancellationToken);
            await _browserService.NavigateToLoginPageAsync(page, cancellationToken);

            if (await _browserService.IsLoggedInAsync(page, cancellationToken))
            {
                _logger.LogInformation("Session for {Username} is already valid — skipping login", username);

                var existingProfile = await _browserService.GetProfileInfoAsync(page, cancellationToken);
                await PersistSessionAsync(accountKey, existingProfile, rememberSession, cancellationToken);

                return new InstagramLoginResult
                {
                    Success = true,
                    Username = existingProfile.Username,
                    DisplayName = existingProfile.DisplayName,
                    ProfilePictureUrl = existingProfile.ProfilePictureUrl,
                    Status = InstagramLoginStatus.Success
                };
            }

            try
            {
                await _browserService.FillLoginFormAsync(page, username, password, cancellationToken);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Could not fill login form automatically for {Username} — waiting for manual login in browser", username);
            }

            var result = await _browserService.WaitForLoginResultAsync(page, username, loginTimeout, cancellationToken);

            if (result.Success)
            {
                await PersistSessionAsync(accountKey, new InstagramProfileInfo
                {
                    Username = result.Username,
                    DisplayName = result.DisplayName,
                    ProfilePictureUrl = result.ProfilePictureUrl,
                    IsLoggedIn = true
                }, rememberSession, cancellationToken);

                _logger.LogInformation("Login successful for {Username}", result.Username);
            }
            else
            {
                _logger.LogWarning("Login failed for {Username}: {Status} — {Error}", username, result.Status, result.Error);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Login cancelled for {Username}", username);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for {Username}", username);
            return new InstagramLoginResult
            {
                Success = false,
                Username = username,
                Status = InstagramLoginStatus.Error,
                Error = ex.Message
            };
        }
        finally
        {
            await _browserService.CloseAsync();
        }
    }

    public async Task<bool> ValidateSessionAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        var profileDirectory = _profileService.GetProfileDirectory(accountKey);
        if (profileDirectory is null)
        {
            _logger.LogWarning("No browser profile found for {AccountKey}", accountKey);
            return false;
        }

        try
        {
            var page = await _browserService.LaunchPersistentContextAsync(profileDirectory, cancellationToken);
            await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _options.NavigationTimeoutSeconds * 1000f
            });

            var isLoggedIn = await _browserService.IsLoggedInAsync(page, cancellationToken);

            await UpdateSessionStatusAsync(accountKey, isLoggedIn, cancellationToken);
            await _browserService.CloseAsync();

            _logger.LogInformation("Session validation for {AccountKey}: {Status}", accountKey, isLoggedIn ? "Valid" : "Invalid");
            return isLoggedIn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session for {AccountKey}", accountKey);
            await _browserService.CloseAsync();
            return false;
        }
    }

    public async Task<InstagramProfileInfo?> GetProfileInfoAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        var profileDirectory = _profileService.GetProfileDirectory(accountKey);
        if (profileDirectory is null)
            return null;

        try
        {
            var page = await _browserService.LaunchPersistentContextAsync(profileDirectory, cancellationToken);
            await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _options.NavigationTimeoutSeconds * 1000f
            });

            var profileInfo = await _browserService.GetProfileInfoAsync(page, cancellationToken);
            await _browserService.CloseAsync();

            return profileInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get profile info for {AccountKey}", accountKey);
            await _browserService.CloseAsync();
            return null;
        }
    }

    public async Task LogoutAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        _logger.LogInformation("Logging out and removing session for {AccountKey}", accountKey);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var session = await context.InstagramAuthSessions
            .FirstOrDefaultAsync(s => s.AccountKey == accountKey, cancellationToken);

        if (session is not null)
        {
            session.IsLoggedIn = false;
            session.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        await _profileService.DeleteProfileAsync(accountKey);
        _logger.LogInformation("Logged out and deleted browser profile for {AccountKey}", accountKey);
    }

    public async Task<IReadOnlyList<InstagramAuthSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.InstagramAuthSessions
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new InstagramAuthSessionDto
            {
                Id = s.Id,
                AccountKey = s.AccountKey,
                Username = s.Username,
                DisplayName = s.DisplayName,
                ProfilePictureUrl = s.ProfilePictureUrl,
                ProfileDirectory = s.ProfileDirectory,
                IsLoggedIn = s.IsLoggedIn,
                RememberSession = s.RememberSession,
                LastLoginAt = s.LastLoginAt,
                LastValidatedAt = s.LastValidatedAt,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<InstagramAuthSessionDto?> GetSessionAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.InstagramAuthSessions
            .AsNoTracking()
            .Where(s => s.AccountKey == accountKey)
            .Select(s => new InstagramAuthSessionDto
            {
                Id = s.Id,
                AccountKey = s.AccountKey,
                Username = s.Username,
                DisplayName = s.DisplayName,
                ProfilePictureUrl = s.ProfilePictureUrl,
                ProfileDirectory = s.ProfileDirectory,
                IsLoggedIn = s.IsLoggedIn,
                RememberSession = s.RememberSession,
                LastLoginAt = s.LastLoginAt,
                LastValidatedAt = s.LastValidatedAt,
                CreatedAt = s.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task PersistSessionAsync(string accountKey, InstagramProfileInfo profileInfo, bool rememberSession, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.InstagramAuthSessions
            .FirstOrDefaultAsync(s => s.AccountKey == accountKey, cancellationToken);

        var profileDirectory = _profileService.GetOrCreateProfileDirectory(accountKey);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            context.InstagramAuthSessions.Add(new InstagramAuthSession
            {
                AccountKey = accountKey,
                Username = profileInfo.Username,
                DisplayName = profileInfo.DisplayName,
                ProfilePictureUrl = profileInfo.ProfilePictureUrl,
                ProfileDirectory = profileDirectory,
                IsLoggedIn = true,
                LastLoginAt = now,
                LastValidatedAt = now,
                RememberSession = rememberSession,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Username = profileInfo.Username;
            existing.DisplayName = profileInfo.DisplayName;
            existing.ProfilePictureUrl = profileInfo.ProfilePictureUrl;
            existing.ProfileDirectory = profileDirectory;
            existing.IsLoggedIn = true;
            existing.LastLoginAt = now;
            existing.LastValidatedAt = now;
            existing.RememberSession = rememberSession;
            existing.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateSessionStatusAsync(string accountKey, bool isLoggedIn, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var session = await context.InstagramAuthSessions
            .FirstOrDefaultAsync(s => s.AccountKey == accountKey, cancellationToken);

        if (session is null)
            return;

        session.IsLoggedIn = isLoggedIn;
        session.LastValidatedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}
