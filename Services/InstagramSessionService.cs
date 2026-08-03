using System.Security.Cryptography;
using System.Text.Json;
using Insgram_Manager.Communication.Models;
using Insgram_Manager.Data;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services;

/// <summary>
/// Persists Instagram communication sessions and interaction history.
/// Sensitive values are protected with ASP.NET Core data protection.
/// </summary>
public sealed class InstagramSessionService : IInstagramSessionService
{
    private const string StoreSettingsKey = "instagram.communication.store";
    private const string ProtectPurpose = "InstagramCommunicationStoreV1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ISettingsService _settingsService;
    private readonly IInstagramCommunicationProgressService _progressService;
    private readonly IDataProtector _protector;
    private readonly ILogger<InstagramSessionService> _logger;

    public InstagramSessionService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ISettingsService settingsService,
        IInstagramCommunicationProgressService progressService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<InstagramSessionService> logger)
    {
        _contextFactory = contextFactory;
        _settingsService = settingsService;
        _progressService = progressService;
        _protector = dataProtectionProvider.CreateProtector(ProtectPurpose);
        _logger = logger;
    }

    public async Task<bool> IsAuthenticatedAsync(string accountKey)
    {
        var session = await GetSessionAsync(accountKey);
        if (session is not { IsAuthenticated: true })
            return false;

        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value <= DateTime.UtcNow)
        {
            _logger.LogWarning("Session for {AccountKey} has expired.", accountKey);
            return false;
        }

        return true;
    }

    public async Task LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Username and password are required for Instagram login.");

        var accountKey = username.ToLowerInvariant();
        var session = new InstagramSessionState
        {
            AccountKey = accountKey,
            Username = username,
            IsAuthenticated = true,
            CreatedAt = DateTime.UtcNow,
            LastAuthenticatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Notes = "Session created via credential login."
        };

        await UpsertSessionAsync(session);
        _logger.LogInformation("Instagram login session stored for user {Username}", username);
    }

    public async Task LogoutAsync(string accountKey)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.InstagramSessions.FirstOrDefaultAsync(s => s.AccountKey == accountKey);
        if (session is null)
            return;

        context.InstagramSessions.Remove(session);
        await context.SaveChangesAsync();
        _logger.LogInformation("Removed Instagram session for account {AccountKey}", accountKey);
    }

    public async Task<string?> GetSessionTokenAsync(string accountKey)
    {
        var session = await GetSessionAsync(accountKey);
        return session?.AccessToken;
    }

    public async Task RefreshSessionAsync(string accountKey)
    {
        var session = await GetSessionAsync(accountKey);
        if (session is null)
            return;

        var refreshed = session with
        {
            LastRefreshedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        await UpsertSessionAsync(refreshed);
    }

    public async Task<InstagramSessionState?> GetSessionAsync(string accountKey)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.InstagramSessions.AsNoTracking().FirstOrDefaultAsync(s => s.AccountKey == accountKey);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<InstagramSessionState>> GetSessionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sessions = await context.InstagramSessions
            .AsNoTracking()
            .OrderByDescending(s => s.LastAuthenticatedAt ?? s.CreatedAt)
            .ToListAsync();

        return sessions.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<InstagramInteractionRecord>> GetInteractionHistoryAsync(int maxRecords = 50)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var interactions = await context.InstagramInteractions
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(Math.Clamp(maxRecords, 1, 500))
            .ToListAsync();

        return interactions.Select(Map).ToList();
    }

    public async Task<InstagramCommunicationStatsDto> GetStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var sessions = await context.InstagramSessions.AsNoTracking().ToListAsync();
        var interactions = await context.InstagramInteractions.AsNoTracking().ToListAsync();

        var now = DateTime.UtcNow;
        return new InstagramCommunicationStatsDto
        {
            TotalSessions = sessions.Count,
            AuthenticatedSessions = sessions.Count(s => s.IsAuthenticated),
            ExpiringSoonSessions = sessions.Count(s => s.ExpiresAt.HasValue && s.ExpiresAt.Value <= now.AddHours(24)),
            TotalInteractions = interactions.Count,
            FailedInteractions = interactions.Count(i => !i.Success),
            LastActivityAt = interactions
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => (DateTime?)i.CreatedAt)
                .FirstOrDefault()
        };
    }

    public async Task<bool> UpsertSessionAsync(InstagramSessionState session)
    {
        if (string.IsNullOrWhiteSpace(session.AccountKey) || string.IsNullOrWhiteSpace(session.Username))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.InstagramSessions.FirstOrDefaultAsync(s => s.AccountKey == session.AccountKey);

        if (entity is null)
        {
            entity = new InstagramSession
            {
                AccountKey = session.AccountKey,
                CreatedAt = session.CreatedAt
            };
            context.InstagramSessions.Add(entity);
        }

        entity.Username = session.Username;
        entity.ProtectedAccessToken = Protect(session.AccessToken);
        entity.ProtectedCookieHeader = Protect(session.CookieHeader);
        entity.ProtectedCsrfToken = Protect(session.CsrfToken);
        entity.IsAuthenticated = session.IsAuthenticated;
        entity.LastAuthenticatedAt = session.LastAuthenticatedAt;
        entity.LastRefreshedAt = session.LastRefreshedAt;
        entity.ExpiresAt = session.ExpiresAt;
        entity.Notes = session.Notes;
        entity.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        await SaveStoreSnapshotAsync(context);
        await _progressService.NotifyCommunicationChangedAsync(session.AccountKey, "Session updated");
        return true;
    }

    public async Task<bool> RecordInteractionAsync(InstagramInteractionRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.AccountKey) || string.IsNullOrWhiteSpace(record.Action) || string.IsNullOrWhiteSpace(record.Endpoint))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.InstagramInteractions.Add(new InstagramInteraction
        {
            AccountKey = record.AccountKey,
            Action = record.Action,
            Method = record.Method,
            Endpoint = record.Endpoint,
            StatusCode = record.StatusCode,
            Success = record.Success,
            Summary = record.Summary,
            Error = record.Error,
            RequestBody = record.RequestBody,
            ResponseBody = record.ResponseBody,
            Duration = record.Duration,
            CreatedAt = record.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        await SaveStoreSnapshotAsync(context);
        await _progressService.NotifyCommunicationChangedAsync(record.AccountKey, "Interaction recorded");
        return true;
    }

    public async Task<bool> StoreSessionWithCookiesAsync(string username, string cookieHeader, string? csrfToken = null, string? accessToken = null)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(cookieHeader))
            return false;

        var accountKey = username.ToLowerInvariant();
        var session = new InstagramSessionState
        {
            AccountKey = accountKey,
            Username = username,
            AccessToken = accessToken,
            CookieHeader = cookieHeader,
            CsrfToken = csrfToken,
            IsAuthenticated = true,
            CreatedAt = DateTime.UtcNow,
            LastAuthenticatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Notes = "Session stored with cookie-based authentication."
        };

        var result = await UpsertSessionAsync(session);
        if (result)
            _logger.LogInformation("Cookie-based session stored for user {Username}", username);

        return result;
    }

    public async Task<bool> ValidateSessionAsync(string accountKey)
    {
        var session = await GetSessionAsync(accountKey);
        if (session is null)
            return false;

        if (!session.IsAuthenticated)
            return false;

        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value <= DateTime.UtcNow)
        {
            _logger.LogWarning("Session for {AccountKey} expired at {ExpiresAt}", accountKey, session.ExpiresAt);
            return false;
        }

        return true;
    }

    private string? Protect(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : _protector.Protect(value);
    }

    private string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return _protector.Unprotect(value);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Instagram session value.");
            return null;
        }
    }

    private InstagramSessionState Map(InstagramSession entity)
    {
        return new InstagramSessionState
        {
            AccountKey = entity.AccountKey,
            Username = entity.Username,
            AccessToken = Unprotect(entity.ProtectedAccessToken),
            CookieHeader = Unprotect(entity.ProtectedCookieHeader),
            CsrfToken = Unprotect(entity.ProtectedCsrfToken),
            IsAuthenticated = entity.IsAuthenticated,
            CreatedAt = entity.CreatedAt,
            LastAuthenticatedAt = entity.LastAuthenticatedAt,
            LastRefreshedAt = entity.LastRefreshedAt,
            ExpiresAt = entity.ExpiresAt,
            Notes = entity.Notes
        };
    }

    private static InstagramInteractionRecord Map(InstagramInteraction entity)
    {
        return new InstagramInteractionRecord
        {
            InteractionId = entity.Id.ToString(),
            AccountKey = entity.AccountKey,
            Action = entity.Action,
            Method = entity.Method,
            Endpoint = entity.Endpoint,
            StatusCode = entity.StatusCode,
            Success = entity.Success,
            Summary = entity.Summary,
            Error = entity.Error,
            RequestBody = entity.RequestBody,
            ResponseBody = entity.ResponseBody,
            CreatedAt = entity.CreatedAt,
            Duration = entity.Duration
        };
    }

    private async Task SaveStoreSnapshotAsync(ApplicationDbContext context)
    {
        // Persist a lightweight JSON snapshot in Settings for quick recovery and portability.
        var sessions = await context.InstagramSessions.AsNoTracking().ToListAsync();
        var interactions = await context.InstagramInteractions
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(200)
            .ToListAsync();

        var snapshot = new InstagramCommunicationStore
        {
            Sessions = sessions.Select(Map).ToList(),
            Interactions = interactions.Select(Map).ToList()
        };

        var protectedJson = _protector.Protect(JsonSerializer.Serialize(snapshot, JsonOptions));
        var existing = await context.Settings.FirstOrDefaultAsync(s => s.Key == StoreSettingsKey);
        if (existing is null)
        {
            context.Settings.Add(new Setting { Key = StoreSettingsKey, Value = protectedJson, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            existing.Value = protectedJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }
}
