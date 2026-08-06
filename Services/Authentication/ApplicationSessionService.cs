using Insgram_Manager.Data;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Interfaces.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Centralized session management — the single source of truth for Instagram authentication.
/// Bridges Playwright-based browser sessions (<see cref="IInstagramLoginService"/>) with
/// the communication session layer (<see cref="IInstagramSessionService"/>).
/// </summary>
public sealed class ApplicationSessionService : IApplicationSessionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IInstagramLoginService _loginService;
    private readonly IInstagramSessionService _sessionService;
    private readonly ILogger<ApplicationSessionService> _logger;

    public ApplicationSessionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IInstagramLoginService loginService,
        IInstagramSessionService sessionService,
        ILogger<ApplicationSessionService> logger)
    {
        _contextFactory = contextFactory;
        _loginService = loginService;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = await GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return false;

        var authSession = await _loginService.GetSessionAsync(accountKey, cancellationToken);
        if (authSession is { IsLoggedIn: true })
            return true;

        return await _sessionService.IsAuthenticatedAsync(accountKey);
    }

    public async Task<string?> GetCurrentAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var playwrightSession = await context.InstagramAuthSessions
            .AsNoTracking()
            .Where(s => s.IsLoggedIn)
            .OrderByDescending(s => s.LastLoginAt ?? s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (playwrightSession is not null)
            return playwrightSession.AccountKey;

        var communicationSessions = await _sessionService.GetSessionsAsync();
        var activeSession = communicationSessions
            .Where(s => s.IsAuthenticated)
            .OrderByDescending(s => s.LastAuthenticatedAt ?? s.CreatedAt)
            .FirstOrDefault();

        return activeSession?.AccountKey;
    }

    public async Task<string?> GetCurrentUsernameAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = await GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var authSession = await context.InstagramAuthSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountKey == accountKey, cancellationToken);

        if (authSession is not null)
            return authSession.Username;

        var commSession = await _sessionService.GetSessionAsync(accountKey);
        return commSession?.Username;
    }

    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = await GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return false;

        try
        {
            var isValid = await _loginService.ValidateSessionAsync(accountKey, cancellationToken);
            if (isValid)
                return true;

            return await _sessionService.ValidateSessionAsync(accountKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session validation failed for {AccountKey}", accountKey);
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = await GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return;

        try
        {
            await _loginService.LogoutAsync(accountKey, cancellationToken);
            await _sessionService.LogoutAsync(accountKey);
            _logger.LogInformation("Logged out session for {AccountKey}", accountKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout for {AccountKey}", accountKey);
        }
    }

    public async Task<DateTime?> GetLastValidationAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = await GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return null;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var authSession = await context.InstagramAuthSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AccountKey == accountKey, cancellationToken);

        return authSession?.LastValidatedAt;
    }
}
