namespace Insgram_Manager.Exceptions;

/// <summary>
/// Exception thrown when domain validation fails.
/// </summary>
public class ValidationException : AppException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public ValidationException(string message, IReadOnlyList<string>? errors = null)
        : base(message, "VALIDATION_ERROR")
    {
        ValidationErrors = errors ?? [];
    }

    public ValidationException(IReadOnlyList<string> errors)
        : base("One or more validation errors occurred.", "VALIDATION_ERROR")
    {
        ValidationErrors = errors;
    }
}
