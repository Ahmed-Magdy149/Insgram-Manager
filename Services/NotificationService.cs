using AntDesign;
using Insgram_Manager.Interfaces;

namespace Insgram_Manager.Services;

/// <summary>
/// Notification service that wraps AntDesign's IMessageService for
/// displaying user-facing notifications.
/// Decouples application components from the UI library's notification API.
/// </summary>
public sealed class NotificationService : Insgram_Manager.Interfaces.INotificationService
{
    private readonly IMessageService _messageService;

    public NotificationService(IMessageService messageService)
    {
        _messageService = messageService;
    }

    public Task NotifyInfoAsync(string title, string message)
    {
        _messageService.Info($"{title}: {message}");
        return Task.CompletedTask;
    }

    public Task NotifySuccessAsync(string title, string message)
    {
        _messageService.Success($"{title}: {message}");
        return Task.CompletedTask;
    }

    public Task NotifyWarningAsync(string title, string message)
    {
        _messageService.Warning($"{title}: {message}");
        return Task.CompletedTask;
    }

    public Task NotifyErrorAsync(string title, string message)
    {
        _messageService.Error($"{title}: {message}");
        return Task.CompletedTask;
    }
}
