using System.Net;
using System.Net.Http.Headers;
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
    private const string WebBaseUrl = "https://www.instagram.com";
    private const string LoginPagePath = "/accounts/login/";
    private const string LoginAjaxPath = "/api/v1/web/accounts/login/ajax/";
    private const string TwoFactorLoginAjaxPath = "/api/v1/web/accounts/login/ajax/two_factor/";
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36";
    private const string InstagramAppId = "936619743392459";

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
    private PendingTwoFactorLogin? _pendingTwoFactorLogin;

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

    private static bool TryCreateChallengeRequired(JsonElement root, out InstagramChallengeRequiredException challengeException)
    {
        challengeException = default!;

        var message = GetString(root, "message");
        var errorType = GetString(root, "error_type");

        var hasChallengeRequiredSignal = string.Equals(message, "challenge_required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorType, "checkpoint_challenge_required", StringComparison.OrdinalIgnoreCase)
            || string.Equals(errorType, "challenge_required", StringComparison.OrdinalIgnoreCase);

        if (!hasChallengeRequiredSignal && !root.TryGetProperty("challenge", out _))
            return false;

        if (!root.TryGetProperty("challenge", out var challengeElement) || challengeElement.ValueKind != JsonValueKind.Object)
            return false;

        var challengeUrl = NormalizeChallengeUrl(GetString(challengeElement, "url") ?? GetString(challengeElement, "api_path"));
        var nativeFlow = challengeElement.TryGetProperty("native_flow", out var nativeFlowElement) && nativeFlowElement.ValueKind == JsonValueKind.True;

        var challengeMessage = nativeFlow
            ? "Instagram requires manual confirmation in the official Instagram app or web flow. No SMS was sent from this request."
            : "Instagram returned a manual checkpoint challenge instead of an SMS verification code.";

        challengeException = new InstagramChallengeRequiredException(challengeUrl, challengeMessage, nativeFlow);
        return true;
    }

    private static string? NormalizeChallengeUrl(string? challengeUrl)
    {
        if (string.IsNullOrWhiteSpace(challengeUrl))
            return null;

        if (Uri.TryCreate(challengeUrl, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        if (challengeUrl.StartsWith('/'))
            return $"{WebBaseUrl}{challengeUrl}";

        return $"{WebBaseUrl}/{challengeUrl.TrimStart('/')}";
    }

    public async Task LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Username and password are required for Instagram login.");

        username = username.Trim();

        var cookieContainer = new CookieContainer();
        using var httpClient = CreateLoginHttpClient(cookieContainer);

        using var loginPageResponse = await httpClient.GetAsync(LoginPagePath, HttpCompletionOption.ResponseHeadersRead);
        loginPageResponse.EnsureSuccessStatusCode();

        var csrfToken = GetCookieValue(cookieContainer, "csrftoken");
        if (string.IsNullOrWhiteSpace(csrfToken))
            throw new InvalidOperationException("Instagram did not return a CSRF token for the login flow.");

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, LoginAjaxPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["enc_password"] = BuildEncryptedPassword(password),
                ["queryParams"] = "{}",
                ["optIntoOneTap"] = "false",
                ["stopDeletionNonce"] = string.Empty,
                ["trustedDeviceRecords"] = "{}"
            })
        };

        loginRequest.Headers.Referrer = new Uri($"{WebBaseUrl}{LoginPagePath}");
        loginRequest.Headers.TryAddWithoutValidation("X-CSRFToken", csrfToken);
        loginRequest.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        loginRequest.Headers.TryAddWithoutValidation("X-IG-App-ID", InstagramAppId);
        loginRequest.Headers.TryAddWithoutValidation("Origin", WebBaseUrl);

        using var loginResponse = await httpClient.SendAsync(loginRequest, HttpCompletionOption.ResponseContentRead);
        var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();
        using var loginDocument = ParseLoginResponse(loginResponse, loginResponseBody);
        var root = loginDocument.RootElement;

        if (TryCreateTwoFactorChallenge(root, username, csrfToken, cookieContainer, out var challenge))
        {
            _pendingTwoFactorLogin = challenge;
            throw new InstagramTwoFactorRequiredException(
                username,
                challenge.TwoFactorIdentifier,
                challenge.ChallengeMessage,
                challenge.AvailableMethods);
        }

        if (TryCreateChallengeRequired(root, out var challengeRequiredException))
            throw challengeRequiredException;

        if (!loginResponse.IsSuccessStatusCode)
            throw BuildLoginFailureException(loginResponse, loginResponseBody, root);

        var authenticated = root.TryGetProperty("authenticated", out var authenticatedElement) && authenticatedElement.GetBoolean();

        if (!authenticated)
            throw BuildLoginFailureException(loginResponse, loginResponseBody, root);

        await StoreAuthenticatedSessionAsync(username, cookieContainer, loginResponse, root, csrfToken, "Session created via Instagram credential login.");
        _pendingTwoFactorLogin = null;
        _logger.LogInformation("Instagram login session stored for user {Username}", username);
    }

    public async Task CompleteTwoFactorLoginAsync(string verificationCode, string? verificationMethod = null)
    {
        if (string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("A two-factor verification code is required.");

        var pending = _pendingTwoFactorLogin;
        if (pending is null)
            throw new InvalidOperationException("There is no pending Instagram two-factor challenge to complete.");

        using var httpClient = CreateLoginHttpClient(pending.CookieContainer);

        using var twoFactorRequest = new HttpRequestMessage(HttpMethod.Post, TwoFactorLoginAjaxPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["identifier"] = pending.TwoFactorIdentifier,
                ["queryParams"] = "{}",
                ["trust_signal"] = "true",
                ["username"] = pending.Username,
                ["verification_method"] = string.IsNullOrWhiteSpace(verificationMethod) ? pending.DefaultVerificationMethod : verificationMethod,
                ["verificationCode"] = verificationCode.Trim()
            })
        };

        twoFactorRequest.Headers.Referrer = new Uri($"{WebBaseUrl}{LoginPagePath}");
        twoFactorRequest.Headers.TryAddWithoutValidation("X-CSRFToken", pending.CsrfToken);
        twoFactorRequest.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        twoFactorRequest.Headers.TryAddWithoutValidation("X-IG-App-ID", InstagramAppId);
        twoFactorRequest.Headers.TryAddWithoutValidation("Origin", WebBaseUrl);

        using var twoFactorResponse = await httpClient.SendAsync(twoFactorRequest, HttpCompletionOption.ResponseContentRead);
        var twoFactorResponseBody = await twoFactorResponse.Content.ReadAsStringAsync();
        using var twoFactorDocument = ParseLoginResponse(twoFactorResponse, twoFactorResponseBody);
        var root = twoFactorDocument.RootElement;

        if (TryCreateChallengeRequired(root, out var challengeRequiredException))
            throw challengeRequiredException;

        if (!twoFactorResponse.IsSuccessStatusCode)
            throw BuildLoginFailureException(twoFactorResponse, twoFactorResponseBody, root);

        var authenticated = root.TryGetProperty("authenticated", out var authenticatedElement) && authenticatedElement.GetBoolean();
        if (!authenticated)
            throw BuildLoginFailureException(twoFactorResponse, twoFactorResponseBody, root);

        await StoreAuthenticatedSessionAsync(pending.Username, pending.CookieContainer, twoFactorResponse, root, pending.CsrfToken, "Session created via Instagram credential login after two-factor verification.");
        _pendingTwoFactorLogin = null;
        _logger.LogInformation("Instagram two-factor login session stored for user {Username}", pending.Username);
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

    private static string BuildEncryptedPassword(string password)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"#PWD_INSTAGRAM_BROWSER:0:{timestamp}:{password}";
    }

    private static string? GetCookieValue(CookieContainer cookieContainer, string cookieName)
    {
        var cookies = cookieContainer.GetCookies(new Uri(WebBaseUrl));
        foreach (Cookie cookie in cookies)
        {
            if (string.Equals(cookie.Name, cookieName, StringComparison.OrdinalIgnoreCase))
                return cookie.Value;
        }

        return null;
    }

    private static HttpClient CreateLoginHttpClient(CookieContainer cookieContainer)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = cookieContainer
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(WebBaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-IG-App-ID", InstagramAppId);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", WebBaseUrl);

        return httpClient;
    }

    private static JsonDocument ParseLoginResponse(HttpResponseMessage response, string responseBody)
    {
        try
        {
            return JsonDocument.Parse(responseBody);
        }
        catch (JsonException ex)
        {
            var snippet = Truncate(responseBody, 800);
            throw new InvalidOperationException($"Instagram login request failed with HTTP {(int)response.StatusCode}. Response: {snippet}", ex);
        }
    }

    private async Task StoreAuthenticatedSessionAsync(
        string username,
        CookieContainer cookieContainer,
        HttpResponseMessage loginResponse,
        JsonElement root,
        string fallbackCsrfToken,
        string notes)
    {
        var accountKey = username.ToLowerInvariant();
        var sessionId = GetCookieValue(cookieContainer, "sessionid") ?? GetCookieValueFromResponse(loginResponse, "sessionid");
        var responseCsrfToken = GetCookieValue(cookieContainer, "csrftoken") ?? GetCookieValueFromResponse(loginResponse, "csrftoken");
        var dsUserId = GetCookieValue(cookieContainer, "ds_user_id")
            ?? GetCookieValueFromResponse(loginResponse, "ds_user_id")
            ?? (root.TryGetProperty("userId", out var userIdElement)
                ? (userIdElement.ValueKind == JsonValueKind.String ? userIdElement.GetString() : userIdElement.GetRawText())
                : null);

        if (string.IsNullOrWhiteSpace(sessionId))
            throw new InvalidOperationException("Instagram login succeeded but no session cookie was returned.");

        responseCsrfToken ??= fallbackCsrfToken;

        var cookieParts = new List<string>
        {
            $"sessionid={sessionId.Trim()}"
        };

        if (!string.IsNullOrWhiteSpace(responseCsrfToken))
            cookieParts.Add($"csrftoken={responseCsrfToken.Trim()}");

        if (!string.IsNullOrWhiteSpace(dsUserId))
            cookieParts.Add($"ds_user_id={dsUserId.Trim().Trim('\"')}");

        var session = new InstagramSessionState
        {
            AccountKey = accountKey,
            Username = username,
            AccessToken = null,
            CookieHeader = string.Join("; ", cookieParts),
            CsrfToken = responseCsrfToken,
            IsAuthenticated = true,
            CreatedAt = DateTime.UtcNow,
            LastAuthenticatedAt = DateTime.UtcNow,
            LastRefreshedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Notes = notes
        };

        var stored = await UpsertSessionAsync(session);
        if (!stored)
            throw new InvalidOperationException("Instagram login succeeded, but the session could not be stored.");
    }

    private static string? GetCookieValueFromResponse(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        foreach (var value in values)
        {
            var prefix = $"{cookieName}=";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var endIndex = value.IndexOf(';');
            return endIndex > 0 ? value[prefix.Length..endIndex] : value[prefix.Length..];
        }

        return null;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength] + "...";
    }

    private static bool TryCreateTwoFactorChallenge(
        JsonElement root,
        string username,
        string csrfToken,
        CookieContainer cookieContainer,
        out PendingTwoFactorLogin challenge)
    {
        challenge = default!;

        var isTwoFactorRequired = root.TryGetProperty("two_factor_required", out var twoFactorRequiredElement) && twoFactorRequiredElement.GetBoolean();
        var errorType = root.TryGetProperty("error_type", out var errorTypeElement)
            ? errorTypeElement.GetString()
            : null;

        if (!isTwoFactorRequired && !string.Equals(errorType, "two_factor_required", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("two_factor_info", out var twoFactorInfoElement) || twoFactorInfoElement.ValueKind != JsonValueKind.Object)
            return false;

        var twoFactorIdentifier = GetString(twoFactorInfoElement, "two_factor_identifier");
        if (string.IsNullOrWhiteSpace(twoFactorIdentifier))
            return false;

        var normalizedUsername = GetString(twoFactorInfoElement, "username") ?? username;
        var availableMethods = GetAvailableTwoFactorMethods(twoFactorInfoElement);
        var challengeMessage = BuildTwoFactorChallengeMessage(normalizedUsername, twoFactorInfoElement, availableMethods);

        challenge = new PendingTwoFactorLogin(
            normalizedUsername,
            twoFactorIdentifier,
            cookieContainer,
            csrfToken,
            challengeMessage,
            availableMethods,
            GetDefaultVerificationMethod(availableMethods));

        return true;
    }

    private static string BuildTwoFactorChallengeMessage(string username, JsonElement twoFactorInfoElement, IReadOnlyList<string> availableMethods)
    {
        var phoneHint = GetString(twoFactorInfoElement, "obfuscated_phone_number_2")
            ?? GetString(twoFactorInfoElement, "obfuscated_phone_number");

        var methodText = availableMethods.Count > 0
            ? $"Available methods: {string.Join(", ", availableMethods.Select(DescribeTwoFactorMethod))}."
            : string.Empty;
        var phoneText = string.IsNullOrWhiteSpace(phoneHint) ? string.Empty : $" Destination: {phoneHint}.";

        return $"Instagram requires a two-factor verification code for {username}.{(string.IsNullOrWhiteSpace(methodText) ? string.Empty : $" {methodText}")}{phoneText}".Trim();
    }

    private static IReadOnlyList<string> GetAvailableTwoFactorMethods(JsonElement twoFactorInfoElement)
    {
        var methods = new List<string>();

        if (GetBoolean(twoFactorInfoElement, "sms_two_factor_on"))
            methods.Add("sms");

        if (GetBoolean(twoFactorInfoElement, "totp_two_factor_on"))
            methods.Add("totp");

        if (GetBoolean(twoFactorInfoElement, "whatsapp_two_factor_on"))
            methods.Add("whatsapp");

        return methods;
    }

    private static string GetDefaultVerificationMethod(IReadOnlyList<string> availableMethods)
    {
        if (availableMethods.Contains("sms", StringComparer.OrdinalIgnoreCase))
            return "1";

        if (availableMethods.Contains("totp", StringComparer.OrdinalIgnoreCase))
            return "3";

        return "6";
    }

    private static string DescribeTwoFactorMethod(string method)
    {
        return method.ToLowerInvariant() switch
        {
            "sms" => "SMS",
            "totp" => "Authenticator app",
            "whatsapp" => "WhatsApp",
            _ => method
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        return property.ValueKind == JsonValueKind.True
            || (property.ValueKind == JsonValueKind.False && property.GetBoolean());
    }

    private static InvalidOperationException BuildLoginFailureException(HttpResponseMessage response, string responseBody, JsonElement root)
    {
        var message = root.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;

        var errorType = root.TryGetProperty("error_type", out var errorTypeElement)
            ? errorTypeElement.GetString()
            : null;

        var detail = string.Join(" ", new[] { message, errorType }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var snippet = Truncate(responseBody, 800);

        return new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
            ? $"Instagram login request failed with HTTP {(int)response.StatusCode}. Response: {snippet}"
            : $"Instagram login request failed with HTTP {(int)response.StatusCode}. {detail}. Response: {snippet}");
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

    private sealed record PendingTwoFactorLogin(
        string Username,
        string TwoFactorIdentifier,
        CookieContainer CookieContainer,
        string CsrfToken,
        string ChallengeMessage,
        IReadOnlyList<string> AvailableMethods,
        string DefaultVerificationMethod);
}

public sealed class InstagramTwoFactorRequiredException : InvalidOperationException
{
    public InstagramTwoFactorRequiredException(
        string username,
        string twoFactorIdentifier,
        string challengeMessage,
        IReadOnlyList<string>? availableMethods = null)
        : base(string.IsNullOrWhiteSpace(challengeMessage)
            ? $"Instagram requires a two-factor verification code for {username}."
            : challengeMessage)
    {
        Username = username;
        TwoFactorIdentifier = twoFactorIdentifier;
        ChallengeMessage = challengeMessage;
        AvailableMethods = availableMethods ?? Array.Empty<string>();
    }

    public string Username { get; }

    public string TwoFactorIdentifier { get; }

    public string ChallengeMessage { get; }

    public IReadOnlyList<string> AvailableMethods { get; }
}

public sealed class InstagramChallengeRequiredException : InvalidOperationException
{
    public InstagramChallengeRequiredException(string? challengeUrl, string challengeMessage, bool nativeFlow)
        : base(string.IsNullOrWhiteSpace(challengeMessage)
            ? "Instagram requires manual confirmation in the official Instagram app or web flow."
            : challengeMessage)
    {
        ChallengeUrl = challengeUrl;
        ChallengeMessage = challengeMessage;
        IsNativeFlow = nativeFlow;
    }

    public string? ChallengeUrl { get; }

    public string ChallengeMessage { get; }

    public bool IsNativeFlow { get; }
}
