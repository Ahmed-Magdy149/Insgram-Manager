namespace Insgram_Manager.Helpers;

/// <summary>
/// Utility helper for file system operations (existence, size, extension validation).
/// </summary>
public static class FileHelper
{
    public static bool Exists(string path) => File.Exists(path);

    public static long GetFileSize(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }

    public static string GetExtension(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant();

    public static bool HasAllowedExtension(string fileName, IReadOnlyList<string> allowedExtensions) =>
        allowedExtensions.Contains(GetExtension(fileName));

    public static async Task<string> ReadAllTextAsync(string path) =>
        await File.ReadAllTextAsync(path);

    public static async Task<byte[]> ReadAllBytesAsync(string path) =>
        await File.ReadAllBytesAsync(path);
}
