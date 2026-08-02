namespace Insgram_Manager.Common.Results;

/// <summary>
/// Represents the outcome of an operation that returns a typed value.
/// Carries Success, Message, Error, and Data in a single immutable object.
/// </summary>
/// <typeparam name="T">The type of the payload data.</typeparam>
public sealed class Result<T>
{
    public T? Data { get; init; }

    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public bool IsFailure => !Success;

    internal Result(T? data, bool success, string? message, string? error)
    {
        Data = data;
        Success = success;
        Message = message;
        Error = error;
    }

    public static Result<T> Ok(T data, string? message = null) =>
        new(data, true, message, null);

    public static Result<T> Fail(string error, string? message = null) =>
        new(default, false, message, error);
}
