using Microsoft.Playwright;

namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Abstraction over Playwright browser operations for Instagram.
/// Manages launching persistent Chromium contexts, navigating pages,
/// and extracting session/profile data.
/// </summary>
public interface IInstagramBrowserService : IAsyncDisposable
{
    /// <summary>
    /// Launches a persistent Chromium browser context using the given profile directory.
    /// The browser state (cookies, localStorage, sessionStorage) is persisted in the profile directory.
    /// </summary>
    Task<IPage> LaunchPersistentContextAsync(string profileDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Navigates to the Instagram login page.
    /// </summary>
    Task NavigateToLoginPageAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills in the username and password fields and clicks the login button.
    /// Does not wait for the result — use <see cref="WaitForLoginResultAsync"/> after calling this.
    /// </summary>
    Task FillLoginFormAsync(IPage page, string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the login process to complete. Detects success, 2FA, challenge, or checkpoint states.
    /// Returns the detected status. If 2FA/challenge is detected, this method waits for the user to
    /// complete the flow in the visible browser window.
    /// </summary>
    Task<InstagramLoginResult> WaitForLoginResultAsync(IPage page, string username, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the browser is currently logged in to Instagram by inspecting cookies and page state.
    /// </summary>
    Task<bool> IsLoggedInAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the Instagram profile information (username, display name, profile picture) from the browser.
    /// </summary>
    Task<InstagramProfileInfo> GetProfileInfoAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the browser context and releases resources.
    /// </summary>
    Task CloseAsync();
}
