using System.Net.Http.Headers;
using System.Text;
using Insgram_Manager.Communication.Models;
using Insgram_Manager.Constants;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insgram_Manager.Services;

/// <summary>
/// Sends authenticated requests against the configured Instagram API base URL.
/// The service does not attempt to bypass platform security; it only forwards
/// requests using a locally managed session and records the outcome.
/// </summary>
public sealed class InstagramRequestService : IInstagramRequestService
{
    private readonly HttpClient _httpClient;
    private readonly IInstagramSessionService _sessionService;
    private readonly IInstagramRateLimiter _rateLimiter;
    private readonly IOptions<InstagramOptions> _options;
    private readonly ILogger<InstagramRequestService> _logger;

    public InstagramRequestService(
        HttpClient httpClient,
        IInstagramSessionService sessionService,
        IInstagramRateLimiter rateLimiter,
        IOptions<InstagramOptions> options,
        ILogger<InstagramRequestService> logger)
    {
        _httpClient = httpClient;
        _sessionService = sessionService;
        _rateLimiter = rateLimiter;
        _options = options;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.Value.ApiBaseUrl ?? InstagramConstants.ApiBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.Value.RequestTimeoutSeconds));
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.Value.DefaultUserAgent ?? InstagramConstants.DefaultUserAgent);
    }

    public async Task<InstagramApiResponse> SendAsync(
        string accountKey,
        HttpMethod method,
        string endpoint,
        HttpContent? content = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        await _rateLimiter.WaitForPermitAsync(cancellationToken);
        var requestBody = content is null ? null : await content.ReadAsStringAsync(cancellationToken);

        var session = await _sessionService.GetSessionAsync(accountKey);
        if (session is null || !session.IsAuthenticated)
        {
            var error = $"Instagram session '{accountKey}' is not authenticated.";
            await _sessionService.RecordInteractionAsync(new InstagramInteractionRecord
            {
                AccountKey = accountKey,
                Action = method.Method,
                Method = method.Method,
                Endpoint = endpoint,
                Success = false,
                Error = error,
                RequestBody = requestBody,
                CreatedAt = startedAt,
                Duration = DateTime.UtcNow - startedAt
            });

            return new InstagramApiResponse
            {
                Success = false,
                Method = method.Method,
                Endpoint = endpoint,
                Error = error,
                Duration = DateTime.UtcNow - startedAt
            };
        }

        using var request = new HttpRequestMessage(method, endpoint);

        if (content is not null)
            request.Content = content;

        if (!string.IsNullOrWhiteSpace(session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        if (!string.IsNullOrWhiteSpace(session.CookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", session.CookieHeader);

        if (!string.IsNullOrWhiteSpace(session.CsrfToken))
            request.Headers.TryAddWithoutValidation("X-CSRFToken", session.CsrfToken);

        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var duration = DateTime.UtcNow - startedAt;
            var success = response.IsSuccessStatusCode;

            await _sessionService.RecordInteractionAsync(new InstagramInteractionRecord
            {
                AccountKey = accountKey,
                Action = method.Method,
                Method = method.Method,
                Endpoint = endpoint,
                StatusCode = (int)response.StatusCode,
                Success = success,
                Summary = success ? "Request completed successfully." : $"Request failed with status {(int)response.StatusCode}.",
                RequestBody = requestBody,
                ResponseBody = Truncate(responseBody),
                CreatedAt = startedAt,
                Duration = duration
            });

            return new InstagramApiResponse
            {
                StatusCode = response.StatusCode,
                Success = success,
                Method = method.Method,
                Endpoint = endpoint,
                Content = responseBody,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startedAt;
            _logger.LogError(ex, "Instagram request failed for {AccountKey} {Method} {Endpoint}", accountKey, method, endpoint);

            await _sessionService.RecordInteractionAsync(new InstagramInteractionRecord
            {
                AccountKey = accountKey,
                Action = method.Method,
                Method = method.Method,
                Endpoint = endpoint,
                Success = false,
                Error = ex.Message,
                RequestBody = requestBody,
                CreatedAt = startedAt,
                Duration = duration
            });

            return new InstagramApiResponse
            {
                Success = false,
                Method = method.Method,
                Endpoint = endpoint,
                Error = ex.Message,
                Duration = duration
            };
        }
    }

    private static string? Truncate(string? value, int maxLength = 8192)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
