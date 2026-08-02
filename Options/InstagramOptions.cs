namespace Insgram_Manager.Options;

/// <summary>
/// Configuration options for Instagram API integration.
/// Bound from the "Instagram" section in appsettings.json.
/// </summary>
public sealed class InstagramOptions
{
    public const string SectionName = "Instagram";

    public string ApiBaseUrl { get; set; } = "https://graph.instagram.com";

    public int RequestTimeoutSeconds { get; set; } = 30;

    public int MaxRequestsPerHour { get; set; } = 200;

    public int RateLimitBufferMs { get; set; } = 1000;

    public string? DefaultUserAgent { get; set; }
}
