namespace Insgram_Manager.Options;

/// <summary>
/// Configuration options for Playwright-based Instagram browser authentication.
/// Bound from the "BrowserAuth" section in appsettings.json.
/// </summary>
public sealed class BrowserAuthOptions
{
    public const string SectionName = "BrowserAuth";

    /// <summary>
    /// Root directory where persistent browser profiles are stored.
    /// Each account gets a subdirectory named after its account key.
    /// </summary>
    public string ProfilesDirectory { get; set; } = "browserprofiles";

    /// <summary>
    /// Whether to run the browser in headless mode.
    /// Set to false during development so the user can interact with 2FA/challenge prompts.
    /// </summary>
    public bool Headless { get; set; } = false;

    /// <summary>
    /// Timeout in seconds to wait for user to complete 2FA or challenge flows in the browser.
    /// </summary>
    public int LoginTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Timeout in seconds for page navigation operations.
    /// </summary>
    public int NavigationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Instagram login page URL.
    /// </summary>
    public string LoginPageUrl { get; set; } = "https://www.instagram.com/accounts/login/";

    /// <summary>
    /// Instagram base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.instagram.com";

    /// <summary>
    /// Channel to use for Playwright Chromium launch.
    /// </summary>
    public string BrowserChannel { get; set; } = "chrome";
}
