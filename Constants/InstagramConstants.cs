namespace Insgram_Manager.Constants;

/// <summary>
/// Constants for Instagram API endpoints, rate limits, and integration metadata.
/// </summary>
public static class InstagramConstants
{
    public const string ApiBaseUrl = "https://graph.instagram.com";

    public const string DefaultUserAgent = "InsgramManager/1.0";

    public const int MaxRequestsPerHour = 200;

    public const int RateLimitBufferMs = 1000;

    public const string ProfileEndpoint = "/me";

    public const string MediaEndpoint = "/me/media";

    public const string FollowersEndpoint = "/me/followers";
}
