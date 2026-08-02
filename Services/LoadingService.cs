using Insgram_Manager.Interfaces;

namespace Insgram_Manager.Services;

/// <summary>
/// Loading service that manages a global loading state.
/// Components can subscribe to LoadingChanged to show/hide a global spinner.
/// Thread-safe via Interlocked for state transitions.
/// </summary>
public sealed class LoadingService : ILoadingService
{
    private int _isLoading;

    public bool IsLoading => Interlocked.CompareExchange(ref _isLoading, 0, 0) == 1;

    public event Action? LoadingChanged;

    public Task ShowAsync()
    {
        Interlocked.Exchange(ref _isLoading, 1);
        LoadingChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        Interlocked.Exchange(ref _isLoading, 0);
        LoadingChanged?.Invoke();
        return Task.CompletedTask;
    }
}
