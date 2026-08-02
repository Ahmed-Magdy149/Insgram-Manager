namespace Insgram_Manager.Exceptions;

/// <summary>
/// Exception thrown when an import operation fails (parsing, reading, validation).
/// </summary>
public class ImportException : AppException
{
    public ImportException(string message, Exception? innerException = null)
        : base(message, "IMPORT_ERROR", innerException)
    {
    }
}
