namespace Insgram_Manager.Helpers;

/// <summary>
/// Utility helper for path normalization, combination, and validation.
/// </summary>
public static class PathHelper
{
    public static string Combine(params string[] segments) =>
        Path.Combine(segments);

    public static string GetFullPath(string relativePath) =>
        Path.GetFullPath(relativePath);

    public static string GetDirectoryName(string path) =>
        Path.GetDirectoryName(path) ?? string.Empty;

    public static string GetFileNameWithoutExtension(string path) =>
        Path.GetFileNameWithoutExtension(path);

    public static string EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }

    public static string GetApplicationRootDirectory() =>
        AppDomain.CurrentDomain.BaseDirectory;
}
