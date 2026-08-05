using System.Text;
using System.Threading;
using Insgram_Manager.Communication.Models;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Services;
using AntDesign;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Data Explorer page.
/// Provides browsing, search, sorting, pagination, column selection, and CSV export for imported relationship data.
/// </summary>
public class DataExplorerPageBase : BaseComponent
{
    private static readonly TimeSpan SessionStatusRefreshInterval = TimeSpan.FromSeconds(20);

    [Inject]
    private IDbContextFactory<Data.ApplicationDbContext> DbContextFactory { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private IPendingRequestService PendingRequestService { get; set; } = default!;

    [Inject]
    private IInstagramSessionService SessionService { get; set; } = default!;

    [Inject]
    private IInstagramRequestService RequestService { get; set; } = default!;

    protected List<InstagramRelationship> FilteredItems { get; set; } = [];
    protected IReadOnlyList<InstagramSessionState> Sessions { get; set; } = [];

    protected string SearchTerm { get; set; } = string.Empty;
    protected RelationshipType SelectedType { get; set; } = RelationshipType.Followers;
    protected List<InstagramRelationship> SelectedRows { get; set; } = [];
    protected string SelectedAccountKey { get; set; } = string.Empty;

    protected int PageIndex { get; set; } = 1;
    protected int PageSize { get; set; } = 10;
    protected int TotalCount { get; set; }
    protected bool IsLoading { get; set; }
    protected long? DeletingRowId { get; set; }
    protected bool IsDeletingSelectedRows { get; set; }

    protected bool ShowUsername { get; set; } = true;
    protected bool ShowFullName { get; set; } = true;
    protected bool ShowProfileUrl { get; set; } = true;
    protected bool ShowType { get; set; } = false;
    protected bool ShowImportedAt { get; set; } = true;
    protected bool ShowCreateSessionForm { get; set; }
    protected bool ShowLoginSessionForm { get; set; }
    protected bool ShowTwoFactorLoginForm { get; set; }
    protected bool IsCreatingSession { get; set; }
    protected bool IsLoggingInSession { get; set; }
    protected bool IsResendingTwoFactorCode { get; set; }
    protected bool IsVerifyingTwoFactorCode { get; set; }
    protected bool IsRefreshingSessionStatus { get; set; }
    protected bool SelectedSessionIsConnected { get; set; }
    protected string SelectedSessionStatusText { get; set; } = "No session selected";
    protected string SelectedSessionStatusColor { get; set; } = "default";
    protected string SelectedSessionStatusIcon { get; set; } = "minus-circle";
    protected string NewSessionUsername { get; set; } = string.Empty;
    protected string NewSessionSessionId { get; set; } = string.Empty;
    protected string NewSessionCsrfToken { get; set; } = string.Empty;
    protected string NewSessionDsUserId { get; set; } = string.Empty;
    protected string NewSessionAccessToken { get; set; } = string.Empty;
    protected string NewSessionCookieHeader { get; set; } = string.Empty;
    protected string NewLoginUsername { get; set; } = string.Empty;
    protected string NewLoginPassword { get; set; } = string.Empty;
    protected string NewTwoFactorCode { get; set; } = string.Empty;
    protected string PendingTwoFactorUsername { get; set; } = string.Empty;
    protected string PendingTwoFactorPassword { get; set; } = string.Empty;
    protected string PendingTwoFactorChallengeMessage { get; set; } = string.Empty;
    protected string PendingTwoFactorVerificationMethod { get; set; } = string.Empty;
    protected IReadOnlyList<string> PendingTwoFactorAvailableMethods { get; set; } = [];
    protected bool ShowInstagramChallengeNotice { get; set; }
    protected string InstagramChallengeUrl { get; set; } = string.Empty;
    protected string InstagramChallengeMessage { get; set; } = string.Empty;

    private string? _lastSearchTerm;
    private CancellationTokenSource? _sessionStatusMonitoringCts;
    private Task? _sessionStatusMonitoringTask;
    private const string InstagramLoginUrl = "https://www.instagram.com/accounts/login/";

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
        await LoadDataAsync();
        await RefreshSelectedSessionStatusAsync(forceCheck: true);
        StartSessionStatusMonitoring();
    }

    protected bool CanCancelOnInstagram =>
        SelectedType == RelationshipType.PendingRequests && !string.IsNullOrWhiteSpace(SelectedAccountKey);

    protected bool IsInstagramSideActionSupported => SelectedType == RelationshipType.PendingRequests;

    protected string InstagramActionLabel => SelectedType == RelationshipType.PendingRequests
        ? "Cancel selected on Instagram"
        : "Instagram action unavailable for this type";

    private async Task LoadSessionsAsync()
    {
        try
        {
            Sessions = await SessionService.GetSessionsAsync();
            if (string.IsNullOrWhiteSpace(SelectedAccountKey) && Sessions.Count > 0)
                SelectedAccountKey = Sessions.First().AccountKey;

            if (!string.IsNullOrWhiteSpace(SelectedAccountKey) && Sessions.All(session => !string.Equals(session.AccountKey, SelectedAccountKey, StringComparison.OrdinalIgnoreCase)) && Sessions.Count > 0)
            {
                SelectedAccountKey = Sessions.First().AccountKey;
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to load Instagram sessions.", ex);
        }
    }

    protected async Task LoginSessionAsync()
    {
        var username = NewLoginUsername.Trim();
        var password = NewLoginPassword;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageService.Warning("Username and password are required to sign in.");
            return;
        }

        IsLoggingInSession = true;
        try
        {
            await SessionService.LoginAsync(username, password);

            SelectedAccountKey = username.ToLowerInvariant();
            await LoadSessionsAsync();
            await RefreshSelectedSessionStatusAsync(forceCheck: true);
            ClearLoginSessionForm();
            ShowLoginSessionForm = false;

            if (SelectedSessionIsConnected)
            {
                MessageService.Success($"Signed in and verified Instagram session for {username}.");
            }
            else
            {
                MessageService.Warning($"Signed in for {username}, but the live verification check did not confirm the session yet.");
            }
        }
        catch (InstagramTwoFactorRequiredException ex)
        {
            ShowInstagramChallengeNotice = false;
            InstagramChallengeUrl = string.Empty;
            InstagramChallengeMessage = string.Empty;
            PendingTwoFactorUsername = ex.Username;
            PendingTwoFactorPassword = password;
            PendingTwoFactorChallengeMessage = ex.ChallengeMessage;
            PendingTwoFactorAvailableMethods = ex.AvailableMethods;
            PendingTwoFactorVerificationMethod = GetPreferredTwoFactorVerificationMethod(ex.AvailableMethods);
            NewTwoFactorCode = string.Empty;
            ShowLoginSessionForm = false;
            ShowTwoFactorLoginForm = true;
            ClearLoginSessionForm();

            MessageService.Warning(string.IsNullOrWhiteSpace(ex.ChallengeMessage)
                ? $"Instagram requires two-factor verification for {ex.Username}."
                : ex.ChallengeMessage);
        }
        catch (InstagramChallengeRequiredException ex)
        {
            ShowTwoFactorLoginForm = false;
            ShowInstagramChallengeNotice = true;
            InstagramChallengeUrl = ex.ChallengeUrl ?? string.Empty;
            InstagramChallengeMessage = ex.ChallengeMessage;
            PendingTwoFactorUsername = username;
            PendingTwoFactorPassword = password;
            PendingTwoFactorChallengeMessage = ex.ChallengeMessage;
            ShowLoginSessionForm = false;
            ClearLoginSessionForm();

            MessageService.Warning(string.IsNullOrWhiteSpace(ex.ChallengeMessage)
                ? "Instagram requires manual confirmation in the app or web flow."
                : ex.ChallengeMessage);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to sign in to Instagram.", ex);
        }
        finally
        {
            IsLoggingInSession = false;
        }
    }

    protected async Task VerifyTwoFactorLoginAsync()
    {
        var verificationCode = NewTwoFactorCode.Trim();

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            MessageService.Warning("A two-factor verification code is required.");
            return;
        }

        IsVerifyingTwoFactorCode = true;
        try
        {
            await SessionService.CompleteTwoFactorLoginAsync(verificationCode, PendingTwoFactorVerificationMethod);

            var username = PendingTwoFactorUsername;
            SelectedAccountKey = username.ToLowerInvariant();
            await LoadSessionsAsync();
            await RefreshSelectedSessionStatusAsync(forceCheck: true);
            ClearTwoFactorLoginForm();
            ShowTwoFactorLoginForm = false;

            if (SelectedSessionIsConnected)
            {
                MessageService.Success($"Two-factor verification completed for {username}. Signed in successfully.");
            }
            else
            {
                MessageService.Warning($"Two-factor verification completed for {username}, but the session is not connected yet.");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to verify the Instagram two-factor code.", ex);
        }
        finally
        {
            IsVerifyingTwoFactorCode = false;
        }
    }

    protected async Task ResendTwoFactorCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(PendingTwoFactorUsername) || string.IsNullOrWhiteSpace(PendingTwoFactorPassword))
        {
            MessageService.Warning("There is no pending Instagram login to resend a code for.");
            return;
        }

        IsResendingTwoFactorCode = true;
        try
        {
            await SessionService.LoginAsync(PendingTwoFactorUsername, PendingTwoFactorPassword);

            SelectedAccountKey = PendingTwoFactorUsername.ToLowerInvariant();
            await LoadSessionsAsync();
            await RefreshSelectedSessionStatusAsync(forceCheck: true);
            ClearTwoFactorLoginForm();
            ShowTwoFactorLoginForm = false;

            MessageService.Success("Instagram login completed after requesting a new code.");
        }
        catch (InstagramTwoFactorRequiredException ex)
        {
            PendingTwoFactorUsername = ex.Username;
            PendingTwoFactorChallengeMessage = ex.ChallengeMessage;
            PendingTwoFactorAvailableMethods = ex.AvailableMethods;
            PendingTwoFactorVerificationMethod = GetPreferredTwoFactorVerificationMethod(ex.AvailableMethods);
            MessageService.Warning("Instagram requested another verification step. If the SMS did not arrive, switch to another available method below.");
        }
        catch (InstagramChallengeRequiredException ex)
        {
            ShowTwoFactorLoginForm = false;
            ShowInstagramChallengeNotice = true;
            InstagramChallengeUrl = ex.ChallengeUrl ?? string.Empty;
            InstagramChallengeMessage = ex.ChallengeMessage;
            MessageService.Warning(string.IsNullOrWhiteSpace(ex.ChallengeMessage)
                ? "Instagram requires manual confirmation in the app or web flow."
                : ex.ChallengeMessage);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to resend the Instagram verification code.", ex);
        }
        finally
        {
            IsResendingTwoFactorCode = false;
        }
    }

    protected void SelectTwoFactorVerificationMethod(string method)
    {
        PendingTwoFactorVerificationMethod = method;
    }

    protected void UseAlternateTwoFactorMethod()
    {
        var alternativeMethod = GetAlternateTwoFactorVerificationMethod();

        if (string.IsNullOrWhiteSpace(alternativeMethod))
        {
            MessageService.Warning("No alternate Instagram verification method is available for this challenge.");
            return;
        }

        PendingTwoFactorVerificationMethod = alternativeMethod;
        NewTwoFactorCode = string.Empty;
        MessageService.Warning($"Switched to {GetTwoFactorMethodDisplayName(alternativeMethod)}. Try the new code path now.");
    }

    protected void DismissInstagramChallengeNotice()
    {
        ShowInstagramChallengeNotice = false;
        InstagramChallengeUrl = string.Empty;
        InstagramChallengeMessage = string.Empty;
    }

    protected bool IsTwoFactorMethodAvailable(string method)
    {
        return PendingTwoFactorAvailableMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
    }

    protected bool IsTwoFactorMethodSelected(string verificationMethod)
    {
        return string.Equals(PendingTwoFactorVerificationMethod, verificationMethod, StringComparison.OrdinalIgnoreCase);
    }

    protected bool CanResendTwoFactorCode =>
        !string.IsNullOrWhiteSpace(PendingTwoFactorPassword) && !IsResendingTwoFactorCode && !IsLoggingInSession;

    protected bool HasAlternateTwoFactorMethod => !string.IsNullOrWhiteSpace(GetAlternateTwoFactorVerificationMethod());

    protected string CurrentTwoFactorMethodDisplayName => GetTwoFactorMethodDisplayName(PendingTwoFactorVerificationMethod);

    protected void SelectSmsVerificationMethod()
    {
        PendingTwoFactorVerificationMethod = "1";
    }

    protected void SelectAuthenticatorVerificationMethod()
    {
        PendingTwoFactorVerificationMethod = "3";
    }

    protected void SelectWhatsappVerificationMethod()
    {
        PendingTwoFactorVerificationMethod = "1";
    }

    private static string GetPreferredTwoFactorVerificationMethod(IEnumerable<string> availableMethods)
    {
        if (availableMethods.Contains("sms", StringComparer.OrdinalIgnoreCase))
            return "1";

        if (availableMethods.Contains("totp", StringComparer.OrdinalIgnoreCase))
            return "3";

        if (availableMethods.Contains("whatsapp", StringComparer.OrdinalIgnoreCase))
            return "1";

        return string.Empty;
    }

    private string? GetAlternateTwoFactorVerificationMethod()
    {
        if (string.Equals(PendingTwoFactorVerificationMethod, "1", StringComparison.OrdinalIgnoreCase))
        {
            if (PendingTwoFactorAvailableMethods.Contains("totp", StringComparer.OrdinalIgnoreCase))
                return "3";
        }

        if (string.Equals(PendingTwoFactorVerificationMethod, "3", StringComparison.OrdinalIgnoreCase))
        {
            if (PendingTwoFactorAvailableMethods.Contains("sms", StringComparer.OrdinalIgnoreCase))
                return "1";
        }

        if (PendingTwoFactorAvailableMethods.Contains("sms", StringComparer.OrdinalIgnoreCase))
            return "1";

        if (PendingTwoFactorAvailableMethods.Contains("totp", StringComparer.OrdinalIgnoreCase))
            return "3";

        return null;
    }

    private static string GetTwoFactorMethodDisplayName(string? method)
    {
        return method?.ToLowerInvariant() switch
        {
            "1" => "SMS",
            "3" => "Authenticator app",
            _ => "Instagram verification"
        };
    }

    protected void ToggleCreateSessionForm()
    {
        ShowCreateSessionForm = !ShowCreateSessionForm;
        if (ShowCreateSessionForm)
        {
            ShowLoginSessionForm = false;
            ShowTwoFactorLoginForm = false;
        }
    }

    protected void CancelCreateSession()
    {
        ShowCreateSessionForm = false;
        ClearCreateSessionForm();
    }

    protected void ToggleLoginSessionForm()
    {
        ShowLoginSessionForm = !ShowLoginSessionForm;
        if (ShowLoginSessionForm)
        {
            ShowCreateSessionForm = false;
            ShowTwoFactorLoginForm = false;
        }
    }

    protected void CancelLoginSession()
    {
        ShowLoginSessionForm = false;
        ClearLoginSessionForm();
    }

    protected async Task OpenInstagramLoginAsync()
    {
        await JSRuntime.InvokeVoidAsync("dataExplorer.openExternalUrl", InstagramLoginUrl);
    }

    protected void ParseSessionCookieHeader()
    {
        var parsed = ParseCookieHeader(NewSessionCookieHeader);

        if (parsed.Count == 0)
        {
            MessageService.Warning("Paste a valid Instagram Cookie header first.");
            return;
        }

        if (parsed.TryGetValue("sessionid", out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
            NewSessionSessionId = sessionId;

        if (parsed.TryGetValue("csrftoken", out var csrfToken) && !string.IsNullOrWhiteSpace(csrfToken))
            NewSessionCsrfToken = csrfToken;

        if (parsed.TryGetValue("ds_user_id", out var dsUserId) && !string.IsNullOrWhiteSpace(dsUserId))
            NewSessionDsUserId = dsUserId;

        if (parsed.TryGetValue("username", out var username) && !string.IsNullOrWhiteSpace(username))
            NewSessionUsername = username;

        MessageService.Success("Parsed Instagram cookies into the session form.");
    }

    protected void CancelTwoFactorLogin()
    {
        ShowTwoFactorLoginForm = false;
        ClearTwoFactorLoginForm();
    }

    protected async Task CreateSessionAsync()
    {
        var username = NewSessionUsername.Trim();
        var rawCookieHeader = NewSessionCookieHeader.Trim();
        var parsedCookies = ParseCookieHeader(rawCookieHeader);

        if (parsedCookies.TryGetValue("username", out var parsedUsername) && string.IsNullOrWhiteSpace(username))
            username = parsedUsername.Trim();

        var sessionId = !string.IsNullOrWhiteSpace(NewSessionSessionId)
            ? NewSessionSessionId.Trim()
            : GetParsedCookieValue(parsedCookies, "sessionid") ?? string.Empty;

        var csrfToken = !string.IsNullOrWhiteSpace(NewSessionCsrfToken)
            ? NewSessionCsrfToken.Trim()
            : GetParsedCookieValue(parsedCookies, "csrftoken");

        var dsUserId = !string.IsNullOrWhiteSpace(NewSessionDsUserId)
            ? NewSessionDsUserId.Trim()
            : GetParsedCookieValue(parsedCookies, "ds_user_id");
        var cookieHeader = BuildCookieHeader(sessionId, csrfToken, dsUserId);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(sessionId))
        {
            MessageService.Warning("Username and session ID are required to create a session. You can paste a raw Cookie header to auto-fill them.");
            return;
        }

        if (string.IsNullOrWhiteSpace(dsUserId))
        {
            MessageService.Warning("DS User ID is required so the app can verify the session live after saving.");
            return;
        }

        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            MessageService.Warning("At least one cookie value is required to create a session.");
            return;
        }

        IsCreatingSession = true;
        try
        {
            var created = await SessionService.StoreSessionWithCookiesAsync(
                username,
                cookieHeader,
                string.IsNullOrWhiteSpace(csrfToken) ? null : csrfToken,
                string.IsNullOrWhiteSpace(NewSessionAccessToken) ? null : NewSessionAccessToken.Trim());

            if (!created)
            {
                MessageService.Error("Failed to create the Instagram session.");
                return;
            }

            SelectedAccountKey = username.ToLowerInvariant();
            await LoadSessionsAsync();
            await RefreshSelectedSessionStatusAsync(forceCheck: true);
            ClearCreateSessionForm();
            ShowCreateSessionForm = false;

            if (SelectedSessionIsConnected)
            {
                MessageService.Success($"Created and verified Instagram session for {username}.");
            }
            else
            {
                MessageService.Warning($"Created Instagram session for {username}, but it does not look connected yet.");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to create the Instagram session.", ex);
        }
        finally
        {
            IsCreatingSession = false;
        }
    }

    private void ClearCreateSessionForm()
    {
        NewSessionUsername = string.Empty;
        NewSessionSessionId = string.Empty;
        NewSessionCsrfToken = string.Empty;
        NewSessionDsUserId = string.Empty;
        NewSessionAccessToken = string.Empty;
        NewSessionCookieHeader = string.Empty;
    }

    private void ClearLoginSessionForm()
    {
        NewLoginUsername = string.Empty;
        NewLoginPassword = string.Empty;
    }

    private void ClearTwoFactorLoginForm()
    {
        NewTwoFactorCode = string.Empty;
        PendingTwoFactorUsername = string.Empty;
        PendingTwoFactorPassword = string.Empty;
        PendingTwoFactorChallengeMessage = string.Empty;
        PendingTwoFactorVerificationMethod = string.Empty;
        PendingTwoFactorAvailableMethods = [];
    }

    private static string BuildCookieHeader(string sessionId, string? csrfToken, string? dsUserId)
    {
        var parts = new List<string>();

        AddCookiePart(parts, "sessionid", sessionId);
        AddCookiePart(parts, "csrftoken", csrfToken);
        AddCookiePart(parts, "ds_user_id", dsUserId);

        return string.Join("; ", parts);
    }

    private static void AddCookiePart(ICollection<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{name}={value.Trim()}");
    }

    private static Dictionary<string, string> ParseCookieHeader(string? cookieHeader)
    {
        var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(cookieHeader))
            return cookies;

        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var name = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(name) && !cookies.ContainsKey(name))
                cookies[name] = value;
        }

        return cookies;
    }

    private static string? GetParsedCookieValue(IReadOnlyDictionary<string, string> cookies, string cookieName)
    {
        return cookies.TryGetValue(cookieName, out var value) ? value : null;
    }

    protected string SelectedSessionDisplayName =>
        Sessions.FirstOrDefault(session => string.Equals(session.AccountKey, SelectedAccountKey, StringComparison.OrdinalIgnoreCase))?.Username
        ?? (string.IsNullOrWhiteSpace(SelectedAccountKey) ? "No session selected" : SelectedAccountKey);

    private void StartSessionStatusMonitoring()
    {
        _sessionStatusMonitoringCts?.Cancel();
        _sessionStatusMonitoringCts?.Dispose();

        _sessionStatusMonitoringCts = new CancellationTokenSource();
        _sessionStatusMonitoringTask = MonitorSessionStatusAsync(_sessionStatusMonitoringCts.Token);
    }

    private async Task MonitorSessionStatusAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SessionStatusRefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await InvokeAsync(async () =>
                {
                    await RefreshSelectedSessionStatusAsync(forceCheck: true);
                    StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshSelectedSessionStatusAsync(bool forceCheck = false)
    {
        if (string.IsNullOrWhiteSpace(SelectedAccountKey))
        {
            SelectedSessionIsConnected = false;
            SelectedSessionStatusText = "No session selected";
            SelectedSessionStatusColor = "default";
            SelectedSessionStatusIcon = "minus-circle";
            return;
        }

        IsRefreshingSessionStatus = true;
        try
        {
            var session = Sessions.FirstOrDefault(item => string.Equals(item.AccountKey, SelectedAccountKey, StringComparison.OrdinalIgnoreCase))
                ?? await SessionService.GetSessionAsync(SelectedAccountKey);

            if (session is null)
            {
                SelectedSessionIsConnected = false;
                SelectedSessionStatusText = "Session not found";
                SelectedSessionStatusColor = "red";
                SelectedSessionStatusIcon = "close-circle";
                return;
            }

            var dsUserId = ExtractCookieValue(session.CookieHeader, "ds_user_id");
            var liveProbe = forceCheck && !string.IsNullOrWhiteSpace(dsUserId)
                ? await RequestService.ProbeSessionAsync(session.AccountKey, $"https://www.instagram.com/api/v1/users/{dsUserId}/info/")
                : null;

            var isConnected = liveProbe ?? await SessionService.ValidateSessionAsync(session.AccountKey);

            SelectedSessionIsConnected = isConnected;
            SelectedSessionStatusText = isConnected ? "Connected" : "Disconnected";
            SelectedSessionStatusColor = isConnected ? "green" : "red";
            SelectedSessionStatusIcon = isConnected ? "check-circle" : "close-circle";
        }
        catch (Exception ex)
        {
            SelectedSessionIsConnected = false;
            SelectedSessionStatusText = "Status check failed";
            SelectedSessionStatusColor = "red";
            SelectedSessionStatusIcon = "close-circle";
            await HandleErrorAsync("Failed to verify the selected Instagram session.", ex);
        }
        finally
        {
            IsRefreshingSessionStatus = false;
        }
    }

    private static string? ExtractCookieValue(string? cookieHeader, string cookieName)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader))
            return null;

        foreach (var part in cookieHeader.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var name = part[..separatorIndex].Trim();
            if (!string.Equals(name, cookieName, StringComparison.OrdinalIgnoreCase))
                continue;

            return part[(separatorIndex + 1)..].Trim();
        }

        return null;
    }

    protected async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            await using var context = await DbContextFactory.CreateDbContextAsync();

            var query = context.InstagramRelationships
                .AsNoTracking()
                .Where(r => r.RelationshipType == SelectedType);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                query = query.Where(r =>
                    r.Username.ToLower().Contains(term) ||
                    (r.FullName != null && r.FullName.ToLower().Contains(term)));
            }

            TotalCount = await query.CountAsync();

            FilteredItems = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((PageIndex - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sessionStatusMonitoringCts?.Cancel();
            _sessionStatusMonitoringCts?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected async Task OnSearch()
    {
        if (_lastSearchTerm == SearchTerm)
            return;

        _lastSearchTerm = SearchTerm;
        PageIndex = 1;
        await LoadDataAsync();
        StateHasChanged();
    }

    protected async Task OnTypeTabChanged(string key)
    {
        if (int.TryParse(key, out var value) && Enum.IsDefined(typeof(RelationshipType), value))
        {
            SelectedType = (RelationshipType)value;
            PageIndex = 1;
            await LoadDataAsync();
            StateHasChanged();
        }
    }

    protected async Task OnTableChange(AntDesign.TableModels.QueryModel<InstagramRelationship> query)
    {
        PageIndex = query.PageIndex;
        PageSize = query.PageSize;
        await LoadDataAsync();
    }

    protected void OnSelectedRowsChanged(IEnumerable<InstagramRelationship> selectedRows)
    {
        SelectedRows = selectedRows.DistinctBy(r => r.Id).ToList();
    }

    protected async Task DeleteSelectedAsync()
    {
        if (SelectedRows.Count == 0)
        {
            MessageService.Warning("Select one or more rows first.");
            return;
        }

        if (!IsInstagramSideActionSupported)
        {
            MessageService.Warning("Instagram-side cancellation is only supported for Pending Requests.");
            return;
        }

        if (Sessions.Count == 0)
        {
            MessageService.Warning("No Instagram sessions are available. Create one from the Communication page first.");
            return;
        }

        var usernames = SelectedRows
            .Select(r => r.Username)
            .Where(username => !string.IsNullOrWhiteSpace(username))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (usernames.Count == 0)
        {
            MessageService.Info("No selected usernames were found.");
            return;
        }

        IsDeletingSelectedRows = true;
        try
        {
            await PendingRequestService.CancelBatchAsync(usernames, SelectedAccountKey);
            SelectedRows.Clear();
            await LoadDataAsync();
            MessageService.Success($"Cancelled {usernames.Count} pending request(s) on Instagram.");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync("Failed to cancel the selected requests on Instagram.", ex);
        }
        finally
        {
            IsDeletingSelectedRows = false;
        }
    }

    protected async Task DeleteRowAsync(InstagramRelationship item)
    {
        if (!IsInstagramSideActionSupported)
        {
            MessageService.Warning("Instagram-side cancellation is only supported for Pending Requests.");
            return;
        }

        if (Sessions.Count == 0)
        {
            MessageService.Warning("No Instagram sessions are available. Create one from the Communication page first.");
            return;
        }

        DeletingRowId = item.Id;
        try
        {
            var result = await PendingRequestService.CancelAsync(item.Username, SelectedAccountKey);
            if (!result.Success)
            {
                await HandleErrorAsync($"Failed to cancel '{item.Username}' on Instagram: {result.Error}");
                return;
            }

            SelectedRows = SelectedRows.Where(r => r.Id != item.Id).ToList();
            await LoadDataAsync();
            MessageService.Success($"Cancelled '{item.Username}' on Instagram.");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync($"Failed to cancel '{item.Username}' on Instagram.", ex);
        }
        finally
        {
            DeletingRowId = null;
        }
    }

    protected string GetTagColor(RelationshipType type)
    {
        return type switch
        {
            RelationshipType.Followers => "blue",
            RelationshipType.Following => "green",
            RelationshipType.PendingRequests => "orange",
            RelationshipType.Blocked => "red",
            RelationshipType.RecentFollowRequests => "purple",
            RelationshipType.RemovedSuggestions => "default",
            _ => "default"
        };
    }

    protected async Task ExportCsvAsync()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var query = context.InstagramRelationships
            .AsNoTracking()
            .Where(r => r.RelationshipType == SelectedType);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Username.ToLower().Contains(term) ||
                (r.FullName != null && r.FullName.ToLower().Contains(term)));
        }

        var exportItems = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var builder = new StringBuilder();
        var headers = new List<string>();

        if (ShowUsername) headers.Add("Username");
        if (ShowFullName) headers.Add("Full Name");
        if (ShowProfileUrl) headers.Add("Profile URL");
        if (ShowType) headers.Add("Type");
        if (ShowImportedAt) headers.Add("Imported At");

        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var item in exportItems)
        {
            var fields = new List<string>();

            if (ShowUsername) fields.Add(EscapeCsv(item.Username));
            if (ShowFullName) fields.Add(EscapeCsv(item.FullName ?? string.Empty));
            if (ShowProfileUrl) fields.Add(EscapeCsv(item.ProfileUrl ?? string.Empty));
            if (ShowType) fields.Add(EscapeCsv(item.RelationshipType.ToString()));
            if (ShowImportedAt) fields.Add(EscapeCsv(item.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

            builder.AppendLine(string.Join(",", fields));
        }

        var csv = builder.ToString();
        var fileName = $"insgram-data-explorer-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await DownloadCsvAsync(fileName, csv);
    }

    private async Task DownloadCsvAsync(string fileName, string csvContent)
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csvContent)).ToArray();
        var base64 = Convert.ToBase64String(bytes);
        await JSRuntime.InvokeVoidAsync("downloadFileFromStream", fileName, $"data:text/csv;charset=utf-8;base64,{base64}");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
