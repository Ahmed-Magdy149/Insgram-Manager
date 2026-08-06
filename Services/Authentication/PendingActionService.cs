using Insgram_Manager.Interfaces.Authentication;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Scoped service that stores a single pending action requiring Instagram authentication.
/// The action is queued before redirecting to login, and dequeued after successful login
/// to automatically restore the previous page and execute the requested operation.
/// </summary>
public sealed class PendingActionService : IPendingActionService
{
    private readonly ILogger<PendingActionService> _logger;
    private PendingAction? _pending;

    public PendingActionService(ILogger<PendingActionService> logger)
    {
        _logger = logger;
    }

    public PendingAction? GetCurrent() => _pending;

    public void Queue(PendingAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _pending = action;
        _logger.LogInformation("Queued pending action {ActionName} for {ReturnUrl}", action.ActionName, action.ReturnUrl);
    }

    public PendingAction? Dequeue()
    {
        var action = _pending;
        _pending = null;
        if (action is not null)
            _logger.LogInformation("Dequeued pending action {ActionName}", action.ActionName);
        return action;
    }

    public void Clear()
    {
        if (_pending is not null)
            _logger.LogInformation("Cleared pending action {ActionName}", _pending.ActionName);
        _pending = null;
    }

    public bool HasPending() => _pending is not null;
}
