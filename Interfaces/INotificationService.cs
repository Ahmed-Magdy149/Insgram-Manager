namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for displaying user-facing notifications.
/// Wraps AntDesign's notification system to decouple UI components
/// from the specific UI library.
/// </summary>
public interface INotificationService
{
    Task NotifyInfoAsync(string title, string message);

    Task NotifySuccessAsync(string title, string message);

    Task NotifyWarningAsync(string title, string message);

    Task NotifyErrorAsync(string title, string message);
}
