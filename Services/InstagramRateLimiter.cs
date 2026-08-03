using Insgram_Manager.Interfaces;
using Insgram_Manager.Options;
using Microsoft.Extensions.Options;

namespace Insgram_Manager.Services;

/// <summary>
/// Simple async rate limiter that spaces Instagram requests based on configured limits.
/// </summary>
public sealed class InstagramRateLimiter : IInstagramRateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeSpan _minimumDelay;
    private DateTime _lastRequestAtUtc = DateTime.MinValue;

    public InstagramRateLimiter(IOptions<InstagramOptions> options)
    {
        var requestsPerHour = Math.Max(1, options.Value.MaxRequestsPerHour);
        _minimumDelay = TimeSpan.FromHours(1d / requestsPerHour);
    }

    public async Task WaitForPermitAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var elapsed = DateTime.UtcNow - _lastRequestAtUtc;
            if (_lastRequestAtUtc != DateTime.MinValue && elapsed < _minimumDelay)
            {
                var delay = _minimumDelay - elapsed;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
            }

            _lastRequestAtUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
