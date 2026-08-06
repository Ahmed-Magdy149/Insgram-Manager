using Insgram_Manager.Components.Shared;
using Insgram_Manager.Interfaces.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Instagram Login page.
/// Manages login form state, login execution, session validation, and profile display.
/// After successful login, restores any pending action and navigates back automatically.
/// </summary>
public class LoginPageBase : AuthGuardComponentBase
{
    [Inject]
    private IInstagramLoginService LoginService { get; set; } = null!;

    [Inject]
    private ILogger<LoginPageBase> Logger { get; set; } = null!;

    protected string Username { get; set; } = string.Empty;
    protected string Password { get; set; } = string.Empty;
    protected bool RememberSession { get; set; } = true;
    protected bool IsLoggingIn { get; set; }
    protected bool IsValidating { get; set; }
    protected bool IsLoggingOut { get; set; }
    protected string? ErrorMessage { get; set; }
    protected string? SuccessMessage { get; set; }

    protected IReadOnlyList<InstagramAuthSessionDto> Sessions { get; set; } = [];
    protected InstagramAuthSessionDto? ActiveSession { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
    }

    protected async Task HandleLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            SuccessMessage = null;
            return;
        }

        IsLoggingIn = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            Logger.LogInformation("Initiating Playwright login for {Username}", Username);

            var result = await LoginService.LoginAsync(Username, Password, RememberSession);

            if (result.Success)
            {
                SuccessMessage = $"Successfully logged in as {result.Username}.";
                Password = string.Empty;
                await LoadSessionsAsync();

                if (PendingActionService.HasPending())
                {
                    var action = PendingActionService.Dequeue();
                    if (action is not null && !string.IsNullOrWhiteSpace(action.ReturnUrl))
                    {
                        Logger.LogInformation("Restoring pending action {ActionName} to {ReturnUrl}",
                            action.ActionName, action.ReturnUrl);
                        NavigationManager.NavigateTo(action.ReturnUrl);
                        return;
                    }
                }
            }
            else
            {
                ErrorMessage = result.Status switch
                {
                    InstagramLoginStatus.TwoFactorRequired => "Two-factor authentication was required but not completed in time. Please try again.",
                    InstagramLoginStatus.ChallengeRequired => "Instagram presented a challenge that was not completed in time. Please try again.",
                    InstagramLoginStatus.CheckpointRequired => "Instagram presented a checkpoint that was not completed in time. Please try again.",
                    InstagramLoginStatus.InvalidCredentials => "Invalid username or password.",
                    InstagramLoginStatus.Timeout => "Login timed out. Please try again.",
                    InstagramLoginStatus.Error => result.Error ?? "An unexpected error occurred during login.",
                    _ => result.Error ?? "Login failed."
                };
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Login was cancelled.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during login for {Username}", Username);
            ErrorMessage = $"An unexpected error occurred: {ex.Message}";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    protected async Task HandleValidateSessionAsync(string accountKey)
    {
        IsValidating = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var isValid = await LoginService.ValidateSessionAsync(accountKey);
            SuccessMessage = isValid
                ? "Session is valid — Instagram is still logged in."
                : "Session is no longer valid. Please log in again.";

            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to validate session for {AccountKey}", accountKey);
            ErrorMessage = $"Failed to validate session: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    protected async Task HandleLogoutAsync(string accountKey)
    {
        IsLoggingOut = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            await LoginService.LogoutAsync(accountKey);
            SuccessMessage = "Logged out and browser profile removed.";
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to logout for {AccountKey}", accountKey);
            ErrorMessage = $"Failed to logout: {ex.Message}";
        }
        finally
        {
            IsLoggingOut = false;
        }
    }

    protected string GetSessionStatusColor(bool isLoggedIn) => isLoggedIn ? "green" : "red";

    protected string GetSessionStatusText(bool isLoggedIn) => isLoggedIn ? "Connected" : "Disconnected";

    protected string FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd HH:mm") ?? "—";

    private async Task LoadSessionsAsync()
    {
        Sessions = await LoginService.GetSessionsAsync();
        ActiveSession = Sessions.FirstOrDefault(s => s.IsLoggedIn);
    }
}
