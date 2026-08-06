using Insgram_Manager.Interfaces.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Components.Shared;

/// <summary>
/// Base component providing Instagram authentication guard functionality.
/// Pages that perform protected Instagram actions inherit from this
/// and call <see cref="RequireInstagramLoginAsync"/> before executing the action.
/// </summary>
/// <remarks>
/// If no valid session exists, the requested action is saved via <see cref="IPendingActionService"/>
/// and the user is redirected to /login. After login, the action is automatically restored.
/// </remarks>
public abstract class AuthGuardComponentBase : BaseComponent
{
    [Inject]
    protected IApplicationSessionService AppSession { get; set; } = default!;

    [Inject]
    protected IPendingActionService PendingActionService { get; set; } = default!;

    [Inject]
    protected ILogger<AuthGuardComponentBase> AuthLogger { get; set; } = default!;

    /// <summary>
    /// Checks if a valid Instagram session exists.
    /// If not, queues the pending action and redirects to /login.
    /// Returns true if the caller should proceed; false if redirected.
    /// </summary>
    protected async Task<bool> RequireInstagramLoginAsync(
        string actionName,
        IReadOnlyList<long>? selectedIds = null,
        IReadOnlyList<string>? selectedUsernames = null,
        IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var isLoggedIn = await AppSession.IsLoggedInAsync(cancellationToken);

        if (isLoggedIn)
        {
            var isValid = await AppSession.ValidateAsync(cancellationToken);
            if (isValid)
                return true;

            AuthLogger.LogInformation("Session expired for action {ActionName} — redirecting to login", actionName);
        }
        else
        {
            AuthLogger.LogInformation("No session for action {ActionName} — redirecting to login", actionName);
        }

        var currentUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var returnUrl = "/" + currentUrl;

        PendingActionService.Queue(new PendingAction
        {
            ActionName = actionName,
            ReturnUrl = returnUrl,
            SelectedIds = selectedIds ?? [],
            SelectedUsernames = selectedUsernames ?? [],
            Parameters = parameters ?? new Dictionary<string, string>()
        });

        NavigationManager.NavigateTo("/login");
        return false;
    }

    /// <summary>
    /// Checks if a pending action exists and should be restored on this page.
    /// Returns the pending action if it matches the current page; otherwise null.
    /// </summary>
    protected PendingAction? TryRestorePendingAction()
    {
        if (!PendingActionService.HasPending())
            return null;

        var action = PendingActionService.GetCurrent();
        if (action is null)
            return null;

        var currentRelativePath = "/" + NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        var returnUrlPath = action.ReturnUrl.Split('?')[0].TrimEnd('/');

        if (!string.Equals(currentRelativePath.TrimEnd('/'), returnUrlPath, StringComparison.OrdinalIgnoreCase))
            return null;

        return PendingActionService.Dequeue();
    }
}
