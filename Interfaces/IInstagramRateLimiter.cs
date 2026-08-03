namespace Insgram_Manager.Interfaces;

/// <summary>
/// Enforces request pacing for Instagram communication calls.
/// </summary>
public interface IInstagramRateLimiter
{
    Task WaitForPermitAsync(CancellationToken cancellationToken = default);
}
