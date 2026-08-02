namespace Insgram_Manager.Exceptions;

/// <summary>
/// Exception thrown when an Instagram API operation fails
/// (authentication, rate limiting, API errors).
/// </summary>
public class InstagramException : AppException
{
    public int? StatusCode { get; }

    public InstagramException(string message, int? statusCode = null, Exception? innerException = null)
        : base(message, "INSTAGRAM_ERROR", innerException)
    {
        StatusCode = statusCode;
    }
}
