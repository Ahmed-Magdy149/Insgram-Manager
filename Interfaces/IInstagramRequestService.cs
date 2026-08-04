using Insgram_Manager.Communication.Models;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Sends authenticated Instagram requests using a locally managed session.
/// </summary>
public interface IInstagramRequestService
{
    Task<InstagramApiResponse> SendAsync(
        string accountKey,
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);

    Task<bool?> ProbeSessionAsync(
        string accountKey,
        string endpoint,
        CancellationToken cancellationToken = default);
}
