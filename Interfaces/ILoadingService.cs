namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for global loading state management.
/// Components can subscribe to LoadingChanged to show/hide a global spinner.
/// </summary>
public interface ILoadingService
{
    bool IsLoading { get; }

    event Action? LoadingChanged;

    Task ShowAsync();

    Task HideAsync();
}
