namespace Insgram_Manager.Common.Results;

/// <summary>
/// Represents the outcome of an operation without a return value.
/// Replaces bool return types across the application for explicit
/// success/failure semantics with optional message and error details.
/// </summary>
public sealed class Result
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public bool IsFailure => !Success;

    private Result(bool success, string? message, string? error)
    {
        Success = success;
        Message = message;
        Error = error;
    }

    public static Result Ok(string? message = null) =>
        new(true, message, null);

    public static Result Fail(string error, string? message = null) =>
        new(false, message, error);

    public static Result<T> Ok<T>(T data, string? message = null) =>
        new(data, true, message, null);

    public static Result<T> Fail<T>(string error, string? message = null) =>
        new(default, false, message, error);
}
