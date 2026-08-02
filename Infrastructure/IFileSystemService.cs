namespace Insgram_Manager.Infrastructure;

/// <summary>
/// Abstraction for file system operations.
/// Enables testability by allowing mock implementations in unit tests.
/// Wraps System.IO for directory, file, and path operations.
/// </summary>
public interface IFileSystemService
{
    // --- Directory operations ---
    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    void DeleteDirectory(string path, bool recursive = true);

    IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern = "*");

    IReadOnlyList<string> GetDirectories(string directoryPath);

    // --- File operations ---
    bool FileExists(string path);

    void DeleteFile(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void CopyFile(string sourcePath, string destinationPath, bool overwrite = false);

    Task<string> ReadAllTextAsync(string path);

    Task<byte[]> ReadAllBytesAsync(string path);

    Task WriteAllTextAsync(string path, string content);

    Task WriteAllBytesAsync(string path, byte[] bytes);

    // --- Path operations ---
    string CombinePath(params string[] segments);

    string GetFileName(string path);

    string GetFileNameWithoutExtension(string path);

    string GetExtension(string path);

    string GetDirectoryName(string path);

    long GetFileSize(string path);
}
