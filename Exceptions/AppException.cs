namespace Insgram_Manager.Exceptions;

/// <summary>
/// Base exception class for all application-specific exceptions.
/// Provides a consistent error code pattern for categorizing failures.
/// </summary>
public class AppException : Exception
{
    public string ErrorCode { get; }

    public AppException(string message, string errorCode = "APP_ERROR", Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
