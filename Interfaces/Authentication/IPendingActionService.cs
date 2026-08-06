namespace Insgram_Manager.Interfaces.Authentication;

/// <summary>
/// Stores a pending action that requires Instagram authentication.
/// After login completes, the action is automatically restored and executed.
/// </summary>
public sealed record PendingAction
{
    /// <summary>
    /// Unique identifier for this pending action.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The action name (e.g. "DeleteSelected", "DeleteAll", "CancelSelected").
    /// </summary>
    public string ActionName { get; init; } = string.Empty;

    /// <summary>
    /// The URL to navigate back to after login completes.
    /// </summary>
    public string ReturnUrl { get; init; } = string.Empty;

    /// <summary>
    /// Selected entity IDs to restore after login.
    /// </summary>
    public IReadOnlyList<long> SelectedIds { get; init; } = [];

    /// <summary>
    /// Selected usernames to restore after login.
    /// </summary>
    public IReadOnlyList<string> SelectedUsernames { get; init; } = [];

    /// <summary>
    /// Additional parameters for the action.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Timestamp when the action was queued.
    /// </summary>
    public DateTime QueuedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Manages pending actions that require Instagram authentication.
/// When a protected action is requested without a valid session,
/// the action is saved here and restored after successful login.
/// </summary>
public interface IPendingActionService
{
    /// <summary>
    /// Returns the current pending action, or null if none.
    /// </summary>
    PendingAction? GetCurrent();

    /// <summary>
    /// Saves a pending action to be executed after login.
    /// </summary>
    void Queue(PendingAction action);

    /// <summary>
    /// Clears and returns the current pending action.
    /// </summary>
    PendingAction? Dequeue();

    /// <summary>
    /// Clears the current pending action without returning it.
    /// </summary>
    void Clear();

    /// <summary>
    /// Returns true if there is a pending action waiting for login.
    /// </summary>
    bool HasPending();
}
