using AntDesign;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Components.Shared;

/// <summary>
/// Reusable base component that centralizes common injected services
/// and shared logic for all page components.
/// Pages inherit from this to avoid repeating @inject directives.
/// </summary>
public abstract class BaseComponent : ComponentBase, IDisposable
{
    private bool _disposed;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected ILogService LogService { get; set; } = default!;

    [Inject]
    protected IMessageService MessageService { get; set; } = default!;

    [Inject]
    protected ModalService ModalService { get; set; } = default!;

    [Inject]
    protected Insgram_Manager.Interfaces.INotificationService NotificationService { get; set; } = default!;

    [Inject]
    protected ILoadingService LoadingService { get; set; } = default!;

    [Inject]
    protected IDialogService DialogService { get; set; } = default!;

    [Inject]
    protected ISettingsService SettingsService { get; set; } = default!;

    /// <summary>
    /// Shows an error message to the user and logs it.
    /// Centralizes error handling to avoid duplication.
    /// </summary>
    protected Task HandleErrorAsync(string userMessage, Exception? exception = null)
    {
        LogService.LogError(userMessage, exception);
        MessageService.Error(userMessage);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a success message to the user.
    /// </summary>
    protected Task NotifySuccessAsync(string message)
    {
        MessageService.Success(message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Dispose(disposing: true);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
