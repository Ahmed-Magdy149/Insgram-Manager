using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Infrastructure;

/// <summary>
/// FileSystemService implementation wrapping System.IO operations.
/// Provides a testable abstraction over direct file system access.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public bool DirectoryExists(string path)
    {
        var exists = Directory.Exists(path);
        _logger.LogDebug("DirectoryExists: {Path} = {Exists}", path, exists);
        return exists;
    }

    public void CreateDirectory(string path)
    {
        _logger.LogDebug("CreateDirectory: {Path}", path);
        Directory.CreateDirectory(path);
    }

    public void DeleteDirectory(string path, bool recursive = true)
    {
        _logger.LogDebug("DeleteDirectory: {Path} (recursive: {Recursive})", path, recursive);
        Directory.Delete(path, recursive);
    }

    public IReadOnlyList<string> GetFiles(string directoryPath, string searchPattern = "*")
    {
        var files = Directory.GetFiles(directoryPath, searchPattern);
        _logger.LogDebug("GetFiles: {Path} ({Count} files)", directoryPath, files.Length);
        return files;
    }

    public IReadOnlyList<string> GetDirectories(string directoryPath)
    {
        var dirs = Directory.GetDirectories(directoryPath);
        _logger.LogDebug("GetDirectories: {Path} ({Count} dirs)", directoryPath, dirs.Length);
        return dirs;
    }

    public bool FileExists(string path)
    {
        var exists = File.Exists(path);
        _logger.LogDebug("FileExists: {Path} = {Exists}", path, exists);
        return exists;
    }

    public void DeleteFile(string path)
    {
        _logger.LogDebug("DeleteFile: {Path}", path);
        File.Delete(path);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        _logger.LogDebug("MoveFile: {Source} -> {Destination}", sourcePath, destinationPath);
        File.Move(sourcePath, destinationPath);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        _logger.LogDebug("CopyFile: {Source} -> {Destination}", sourcePath, destinationPath);
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        _logger.LogDebug("ReadAllText: {Path}", path);
        return await File.ReadAllTextAsync(path);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path)
    {
        _logger.LogDebug("ReadAllBytes: {Path}", path);
        return await File.ReadAllBytesAsync(path);
    }

    public async Task WriteAllTextAsync(string path, string content)
    {
        _logger.LogDebug("WriteAllText: {Path}", path);
        await File.WriteAllTextAsync(path, content);
    }

    public async Task WriteAllBytesAsync(string path, byte[] bytes)
    {
        _logger.LogDebug("WriteAllBytes: {Path}", path);
        await File.WriteAllBytesAsync(path, bytes);
    }

    public string CombinePath(params string[] segments) =>
        Path.Combine(segments);

    public string GetFileName(string path) =>
        Path.GetFileName(path);

    public string GetFileNameWithoutExtension(string path) =>
        Path.GetFileNameWithoutExtension(path);

    public string GetExtension(string path) =>
        Path.GetExtension(path);

    public string GetDirectoryName(string path) =>
        Path.GetDirectoryName(path) ?? string.Empty;

    public long GetFileSize(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }
}
