namespace Insgram_Manager.Infrastructure;

/// <summary>
/// Abstraction for ZIP archive extraction, validation, and directory management.
/// Used by the Import module to extract Instagram data export archives.
/// </summary>
public interface IZipExtractionService
{
    /// <summary>
    /// Validates that the specified file is a valid ZIP archive.
    /// </summary>
    Task<bool> IsValidZipAsync(string archivePath);

    /// <summary>
    /// Extracts a ZIP archive to the specified destination directory.
    /// Returns the full path to the extraction directory.
    /// </summary>
    Task<string> ExtractAsync(string archivePath, string destinationPath);

    /// <summary>
    /// Extracts a ZIP archive to a temporary directory and returns the path.
    /// Caller is responsible for cleaning up the temporary directory.
    /// </summary>
    Task<string> ExtractToTempAsync(string archivePath);

    /// <summary>
    /// Lists the entry names contained within a ZIP archive without extracting.
    /// </summary>
    Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath);
}
