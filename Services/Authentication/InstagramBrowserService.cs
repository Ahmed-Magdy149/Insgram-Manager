using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Playwright-based Instagram browser service.
/// Launches persistent Chromium contexts, navigates pages, fills login forms,
/// detects login state, and extracts profile information.
/// Never launches incognito — always uses persistent contexts to preserve session state.
/// </summary>
public sealed class InstagramBrowserService : IInstagramBrowserService, IAsyncDisposable
{
    private readonly BrowserAuthOptions _options;
    private readonly ILogger<InstagramBrowserService> _logger;
    private IPlaywright? _playwright;
    private IBrowserContext? _browserContext;
    private IBrowser? _browser;
    private bool _disposed;

    public InstagramBrowserService(IOptions<BrowserAuthOptions> options, ILogger<InstagramBrowserService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IPage> LaunchPersistentContextAsync(string profileDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Directory.Exists(profileDirectory))
            Directory.CreateDirectory(profileDirectory);

        _playwright = await Playwright.CreateAsync();

        _browserContext = await _playwright.Chromium.LaunchPersistentContextAsync(profileDirectory, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = _options.Headless,
            Channel = _options.BrowserChannel,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            Locale = "en-US",
            TimezoneId = "America/New_York",
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--no-default-browser-check"
            ]
        });

        var page = _browserContext.Pages.FirstOrDefault() ?? await _browserContext.NewPageAsync();
        page.SetDefaultTimeout(_options.NavigationTimeoutSeconds * 1000f);

        _logger.LogInformation("Launched persistent Chromium context at {ProfileDirectory}", profileDirectory);
        return page;
    }

    public async Task NavigateToLoginPageAsync(IPage page, CancellationToken cancellationToken = default)
    {
        await page.GotoAsync(_options.LoginPageUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = _options.NavigationTimeoutSeconds * 1000f
        });

        _logger.LogInformation("Navigated to Instagram login page");
    }

    public async Task FillLoginFormAsync(IPage page, string username, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var selectorTimeout = _options.LoginTimeoutSeconds * 1000f;

        await WaitForPageSettledAsync(page, selectorTimeout, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var usernameSelectors = new[]
        {
            "input[name='username']",
            "input[aria-label='Phone number, username, or email']",
            "input[autocomplete='username']",
            "input[id='username']"
        };

        var passwordSelectors = new[]
        {
            "input[name='password']",
            "input[aria-label='Password']",
            "input[type='password']",
            "input[autocomplete='current-password']"
        };

        var usernameField = await WaitForAnySelectorAsync(page, usernameSelectors, selectorTimeout);
        if (usernameField is null)
        {
            var currentUrl = page.Url;
            throw new TimeoutException(
                $"Could not find the Instagram username field. The page may have redirected to Facebook login ({currentUrl}). " +
                "If you see a Facebook login page, complete the login there manually — the system will detect when you're logged in.");
        }

        await usernameField.FillAsync(username);
        cancellationToken.ThrowIfCancellationRequested();

        var passwordField = await WaitForAnySelectorAsync(page, passwordSelectors, selectorTimeout);
        if (passwordField is null)
            throw new TimeoutException("Could not find the Instagram password field.");

        await passwordField.FillAsync(password);
        cancellationToken.ThrowIfCancellationRequested();

        var submitSelectors = new[]
        {
            "button[type='submit']",
            "button:has-text('Log in')",
            "button:has-text('Log In')",
            "div[role='button']:has-text('Log in')"
        };

        var submitButton = await WaitForAnySelectorAsync(page, submitSelectors, 10000f);
        if (submitButton is null)
            throw new TimeoutException("Could not find the Instagram submit button.");

        await submitButton.ClickAsync();

        _logger.LogInformation("Filled login form for username: {Username}", username);
    }

    private async Task WaitForPageSettledAsync(IPage page, float timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
        }

        for (var i = 0; i < 5; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var urlBefore = page.Url;
            await page.WaitForTimeoutAsync(1000);
            if (page.Url == urlBefore)
                break;
        }
    }

    private static async Task<IElementHandle?> WaitForAnySelectorAsync(IPage page, string[] selectors, float timeoutMs)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element is not null && await element.IsVisibleAsync())
                        return element;
                }
                catch
                {
                }
            }

            await page.WaitForTimeoutAsync(500);
        }

        return null;
    }

    public async Task<InstagramLoginResult> WaitForLoginResultAsync(IPage page, string username, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        var status = InstagramLoginStatus.Error;
        string? error = null;
        var logged2fa = false;
        var loggedChallenge = false;

        try
        {
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await page.WaitForTimeoutAsync(1000);

                var currentUrl = page.Url;

                if (await IsOnTwoFactorPageAsync(page))
                {
                    if (!logged2fa)
                    {
                        status = InstagramLoginStatus.TwoFactorRequired;
                        _logger.LogInformation("2FA required for {Username} — waiting for user to complete in browser", username);
                        logged2fa = true;
                    }
                    continue;
                }

                if (await IsOnChallengePageAsync(page))
                {
                    if (!loggedChallenge)
                    {
                        status = InstagramLoginStatus.ChallengeRequired;
                        _logger.LogInformation("Challenge required for {Username} — waiting for user to complete in browser", username);
                        loggedChallenge = true;
                    }
                    continue;
                }

                if (await IsInvalidCredentialsAsync(page))
                {
                    status = InstagramLoginStatus.InvalidCredentials;
                    error = "Invalid username or password.";
                    break;
                }

                if (await IsLoggedInAsync(page, cancellationToken))
                {
                    status = InstagramLoginStatus.Success;
                    break;
                }

                if (currentUrl.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) &&
                    !currentUrl.Contains("/login.php", StringComparison.OrdinalIgnoreCase) &&
                    !currentUrl.Contains("/two_step", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("User appears to have completed Facebook login — navigating to Instagram to verify for {Username}", username);
                    try
                    {
                        await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.NetworkIdle,
                            Timeout = _options.NavigationTimeoutSeconds * 1000f
                        });
                    }
                    catch
                    {
                    }
                    await page.WaitForTimeoutAsync(2000);

                    if (await IsLoggedInAsync(page, cancellationToken))
                    {
                        status = InstagramLoginStatus.Success;
                        break;
                    }
                }
            }

            if (status == InstagramLoginStatus.Error && DateTime.UtcNow >= deadline)
            {
                status = InstagramLoginStatus.Timeout;
                error = "Login timed out waiting for completion. If you completed login in the browser, try validating the session from the Login page.";
            }
        }
        catch (OperationCanceledException)
        {
            status = InstagramLoginStatus.Timeout;
            error = "Login was cancelled.";
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login wait for {Username}", username);
            status = InstagramLoginStatus.Error;
            error = ex.Message;
        }

        InstagramProfileInfo? profileInfo = null;
        if (status == InstagramLoginStatus.Success)
        {
            try
            {
                profileInfo = await GetProfileInfoAsync(page, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract profile info after successful login for {Username}", username);
            }
        }

        return new InstagramLoginResult
        {
            Success = status == InstagramLoginStatus.Success,
            Username = profileInfo?.Username ?? username,
            DisplayName = profileInfo?.DisplayName,
            ProfilePictureUrl = profileInfo?.ProfilePictureUrl,
            Status = status,
            Error = error
        };
    }

    public async Task<bool> IsLoggedInAsync(IPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cookies = await page.Context.CookiesAsync();

            var hasSessionId = cookies.Any(c =>
                string.Equals(c.Name, "sessionid", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(c.Value));

            var hasDsUserId = cookies.Any(c =>
                string.Equals(c.Name, "ds_user_id", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(c.Value));

            if (!hasSessionId && !hasDsUserId)
                return false;

            var currentUrl = page.Url;
            if (currentUrl.Contains("/accounts/login", StringComparison.OrdinalIgnoreCase) ||
                currentUrl.Contains("/challenge/", StringComparison.OrdinalIgnoreCase) ||
                currentUrl.Contains("/two_factor", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check login status");
            return false;
        }
    }

    public async Task<InstagramProfileInfo> GetProfileInfoAsync(IPage page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isLoggedIn = await IsLoggedInAsync(page, cancellationToken);
        if (!isLoggedIn)
            return new InstagramProfileInfo { IsLoggedIn = false };

        var currentUrl = page.Url;
        if (!currentUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = _options.NavigationTimeoutSeconds * 1000f
                });
                await page.WaitForTimeoutAsync(2000);
            }
            catch
            {
            }
        }

        var username = await ExtractUsernameAsync(page);
        var displayName = await ExtractDisplayNameAsync(page);
        var profilePictureUrl = await ExtractProfilePictureUrlAsync(page);

        return new InstagramProfileInfo
        {
            Username = username,
            DisplayName = displayName,
            ProfilePictureUrl = profilePictureUrl,
            IsLoggedIn = true
        };
    }

    public async Task CloseAsync()
    {
        if (_browserContext is not null)
        {
            await _browserContext.CloseAsync();
            _browserContext = null;
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        _logger.LogInformation("Closed Instagram browser context");
    }

    private async Task<InstagramLoginStatus> WaitForUserCompletionAsync(IPage page, string username, TimeSpan remaining, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + remaining;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.WaitForTimeoutAsync(1000);

            if (await IsLoggedInAsync(page, cancellationToken))
                return InstagramLoginStatus.Success;

            var currentUrl = page.Url;
            if (currentUrl.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) &&
                !currentUrl.Contains("/login.php", StringComparison.OrdinalIgnoreCase) &&
                !currentUrl.Contains("/two_step", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Facebook login detected — navigating to Instagram to verify for {Username}", username);
                try
                {
                    await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = _options.NavigationTimeoutSeconds * 1000f
                    });
                }
                catch
                {
                }
                await page.WaitForTimeoutAsync(2000);

                if (await IsLoggedInAsync(page, cancellationToken))
                    return InstagramLoginStatus.Success;
            }

            if (await IsInvalidCredentialsAsync(page))
                return InstagramLoginStatus.InvalidCredentials;
        }

        return InstagramLoginStatus.Timeout;
    }

    private static async Task<bool> IsOnTwoFactorPageAsync(IPage page)
    {
        var url = page.Url;
        if (url.Contains("/two_factor", StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var securityCodeInput = await page.QuerySelectorAsync("input[name='verificationCode']");
            return securityCodeInput is not null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsOnChallengePageAsync(IPage page)
    {
        var url = page.Url;
        return url.Contains("/challenge/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("/checkpoint", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsInvalidCredentialsAsync(IPage page)
    {
        try
        {
            var errorElement = await page.QuerySelectorAsync("[role='alert']");
            if (errorElement is not null)
            {
                var text = await errorElement.TextContentAsync();
                if (text is not null &&
                    (text.Contains("Sorry, your password was incorrect", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("The username you entered", StringComparison.OrdinalIgnoreCase) ||
                     text.Contains("incorrect", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static async Task<string> ExtractUsernameAsync(IPage page)
    {
        try
        {
            var cookies = await page.Context.CookiesAsync();
            var dsUserId = cookies.FirstOrDefault(c => string.Equals(c.Name, "ds_user_id", StringComparison.OrdinalIgnoreCase));
            if (dsUserId is not null && !string.IsNullOrEmpty(dsUserId.Value))
                return dsUserId.Value;

            var url = page.Url;
            if (url.Contains("instagram.com/") && !url.Contains("/accounts/"))
            {
                var segments = new Uri(url).Segments;
                if (segments.Length > 1)
                    return segments[1].TrimEnd('/');
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static async Task<string?> ExtractDisplayNameAsync(IPage page)
    {
        try
        {
            var headerElement = await page.QuerySelectorAsync("header h1, header h2");
            if (headerElement is not null)
            {
                var text = await headerElement.TextContentAsync();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<string?> ExtractProfilePictureUrlAsync(IPage page)
    {
        try
        {
            var profileImg = await page.QuerySelectorAsync("header img, img[alt*='profile']");
            if (profileImg is not null)
            {
                var src = await profileImg.GetAttributeAsync("src");
                if (!string.IsNullOrWhiteSpace(src))
                    return src;
            }
        }
        catch
        {
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAsync();
    }
}
