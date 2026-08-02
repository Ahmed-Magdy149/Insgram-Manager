using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Infrastructure;

/// <summary>
/// ZIP extraction service implementation using System.IO.Compression.
/// Provides validation, extraction, and entry listing for ZIP archives.
/// </summary>
public sealed class ZipExtractionService : IZipExtractionService
{
    private readonly ILogger<ZipExtractionService> _logger;

    public ZipExtractionService(ILogger<ZipExtractionService> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsValidZipAsync(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            _logger.LogWarning("ZIP file not found: {Path}", archivePath);
            return Task.FromResult(false);
        }

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            _logger.LogDebug("Validated ZIP archive: {Path} ({Count} entries)", archivePath, archive.Entries.Count);
            return Task.FromResult(true);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid ZIP archive: {Path}", archivePath);
            return Task.FromResult(false);
        }
    }

    public async Task<string> ExtractAsync(string archivePath, string destinationPath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("ZIP archive not found.", archivePath);

        Directory.CreateDirectory(destinationPath);

        _logger.LogInformation("Extracting ZIP: {Path} -> {Destination}", archivePath, destinationPath);

        await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, destinationPath, overwriteFiles: true));

        _logger.LogInformation("Extraction complete: {Destination}", destinationPath);
        return destinationPath;
    }

    public async Task<string> ExtractToTempAsync(string archivePath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "InsgramManager_" + Guid.NewGuid().ToString("N"));
        _logger.LogDebug("Extracting ZIP to temp: {TempPath}", tempPath);
        return await ExtractAsync(archivePath, tempPath);
    }

    public Task<IReadOnlyList<string>> ListEntriesAsync(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("ZIP archive not found.", archivePath);

        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        _logger.LogDebug("Listed {Count} entries in ZIP: {Path}", entries.Count, archivePath);
        return Task.FromResult<IReadOnlyList<string>>(entries);
    }
}
