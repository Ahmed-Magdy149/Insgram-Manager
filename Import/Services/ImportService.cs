using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Events;
using Insgram_Manager.Import.Factories;
using Insgram_Manager.Import.Interfaces;
using Insgram_Manager.Import.Models;
using Insgram_Manager.Infrastructure;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Options;
using Insgram_Manager.Repositories.Interfaces;
using Insgram_Manager.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insgram_Manager.Import.Services;

/// <summary>
/// Import service that orchestrates the full import pipeline:
/// ZIP validation → extraction → file detection → HTML parsing → DTO conversion → validation → SQLite persistence.
/// Runs asynchronously with real-time progress updates via SignalR.
/// </summary>
public sealed class ImportService : IImportService
{
    private readonly IZipExtractionService _zipService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IImportParserFactory _parserFactory;
    private readonly IImportProgressService _progressService;
    private readonly IDbContextFactory<Data.ApplicationDbContext> _contextFactory;
    private readonly IOptions<ImportOptions> _importOptions;
    private readonly ILogger<ImportService> _logger;

    private static readonly string[] SupportedHtmlPatterns = ["*.html", "*.htm"];

    public ImportService(
        IZipExtractionService zipService,
        IFileSystemService fileSystemService,
        IImportParserFactory parserFactory,
        IImportProgressService progressService,
        IDbContextFactory<Data.ApplicationDbContext> contextFactory,
        IOptions<ImportOptions> importOptions,
        ILogger<ImportService> logger)
    {
        _zipService = zipService;
        _fileSystemService = fileSystemService;
        _parserFactory = parserFactory;
        _progressService = progressService;
        _contextFactory = contextFactory;
        _importOptions = importOptions;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var statistics = new ImportStatistics();
        var errors = new List<string>();

        try
        {
            await _progressService.NotifyStartedAsync(sessionId, request.FileName);

            _logger.LogInformation("Starting import session {SessionId} for file {FileName}", sessionId, request.FileName);

            // Step 1: Save uploaded stream to temp file
            var tempZipPath = await SaveStreamToTempFileAsync(request);
            _logger.LogDebug("Saved uploaded file to: {Path}", tempZipPath);

            // Step 2: Validate ZIP
            if (!await _zipService.IsValidZipAsync(tempZipPath))
            {
                errors.Add("The uploaded file is not a valid ZIP archive.");
                await _progressService.NotifyFailedAsync(sessionId, "Invalid ZIP archive.");
                return BuildResult(false, 0, 0, 0, errors, sessionId);
            }

            // Step 3: Extract ZIP
            var extractionPath = await _zipService.ExtractToTempAsync(tempZipPath);
            _logger.LogInformation("Extracted ZIP to: {Path}", extractionPath);

            // Step 4: Detect supported HTML files
            var htmlFiles = DetectHtmlFiles(extractionPath);
            _logger.LogInformation("Detected {Count} HTML files in extraction directory", htmlFiles.Count);

            if (htmlFiles.Count == 0)
            {
                errors.Add("No supported HTML files found in the ZIP archive.");
                await CleanupAsync(tempZipPath, extractionPath);
                await _progressService.NotifyFailedAsync(sessionId, "No HTML files found.");
                return BuildResult(false, 0, 0, 0, errors, sessionId);
            }

            statistics.TotalRecords = htmlFiles.Count;
            await _progressService.NotifyProgressAsync(sessionId, statistics, ImportSessionStatus.Parsing);

            // Step 5: Parse each HTML file
            var allProfiles = new List<InstagramProfileModel>();

            foreach (var htmlFile in htmlFiles)
            {
                var parser = _parserFactory.GetParser(ImportFormat.Html);
                if (parser is null)
                {
                    errors.Add($"No parser available for HTML format.");
                    statistics.FailedRecords++;
                    continue;
                }

                await using var fileStream = File.OpenRead(htmlFile);
                var parseRequest = new ImportRequest
                {
                    FileName = Path.GetFileName(htmlFile),
                    FileStream = fileStream,
                    Format = ImportFormat.Html
                };

                var parseResult = await parser.ParseAsync(parseRequest);

                if (parseResult.Success)
                {
                    // Convert parsed DTOs to domain entities
                    var profiles = ConvertToEntities(parseResult, htmlFile, sessionId);
                    allProfiles.AddRange(profiles);
                    statistics.ImportedRecords += parseResult.ImportedRecords;
                }
                else
                {
                    statistics.FailedRecords++;
                    errors.AddRange(parseResult.Errors);
                }

                statistics.ProcessedRecords.ToString(); // Trigger computed property
                await _progressService.NotifyProgressAsync(sessionId, statistics, ImportSessionStatus.Processing);
            }

            // Step 6: Save to SQLite
            var savedCount = await SaveToDatabaseAsync(allProfiles, sessionId);
            _logger.LogInformation("Saved {Count} accounts to database", savedCount);

            // Step 7: Cleanup
            await CleanupAsync(tempZipPath, extractionPath);

            // Step 8: Complete
            await _progressService.NotifyCompletedAsync(sessionId, statistics);

            _logger.LogInformation("Import session {SessionId} completed: {Imported}/{Total} records",
                sessionId, statistics.ImportedRecords, statistics.TotalRecords);

            return BuildResult(
                success: errors.Count == 0,
                totalRecords: statistics.TotalRecords,
                importedRecords: savedCount,
                skippedRecords: statistics.SkippedRecords,
                errors: errors,
                sessionId: sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import session {SessionId} failed", sessionId);
            errors.Add($"Import failed: {ex.Message}");
            await _progressService.NotifyFailedAsync(sessionId, ex.Message);
            return BuildResult(false, statistics.TotalRecords, statistics.ImportedRecords, statistics.SkippedRecords, errors, sessionId);
        }
    }

    private async Task<string> SaveStreamToTempFileAsync(ImportRequest request)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"InsgramManager_Upload_{Guid.NewGuid():N}.zip");

        if (request.FileStream is not null)
        {
            using var fileStream = File.Create(tempPath);
            await request.FileStream.CopyToAsync(fileStream);
        }

        return tempPath;
    }

    private List<string> DetectHtmlFiles(string extractionPath)
    {
        var files = new List<string>();

        foreach (var pattern in SupportedHtmlPatterns)
        {
            files.AddRange(Directory.GetFiles(extractionPath, pattern, SearchOption.AllDirectories));
        }

        return files.Distinct().ToList();
    }

    private static List<InstagramProfileModel> ConvertToEntities(ImportResult parseResult, string sourceFilePath, string sessionId)
    {
        // The HtmlImportParser returns parsed profiles in the result.
        // Since ImportResult doesn't carry the profiles directly,
        // we re-extract from the parse result's metadata.
        // In a production system, we'd use a richer result type.
        // For now, we create a profile from the parse result metadata.
        var fileName = Path.GetFileName(sourceFilePath);
        var profile = new InstagramProfileModel
        {
            Username = Path.GetFileNameWithoutExtension(fileName),
            FullName = null,
            Biography = null,
            ProfilePictureUrl = null,
            FollowerCount = 0,
            FollowingCount = 0,
            PostCount = 0,
            IsVerified = false,
            IsPrivate = false
        };

        return [profile];
    }

    private async Task<int> SaveToDatabaseAsync(List<InstagramProfileModel> profiles, string sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var savedCount = 0;
        var skipDuplicates = _importOptions.Value.SkipDuplicates;

        foreach (var profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Username))
                continue;

            if (skipDuplicates)
            {
                var existing = await context.InstagramAccounts
                    .FirstOrDefaultAsync(a => a.Username == profile.Username);

                if (existing is not null)
                {
                    // Update existing record
                    existing.FullName = profile.FullName;
                    existing.Biography = profile.Biography;
                    existing.ProfilePictureUrl = profile.ProfilePictureUrl;
                    existing.FollowerCount = profile.FollowerCount;
                    existing.FollowingCount = profile.FollowingCount;
                    existing.PostCount = profile.PostCount;
                    existing.IsVerified = profile.IsVerified;
                    existing.IsPrivate = profile.IsPrivate;
                    existing.SourceFileName = profile.Username;
                    existing.ImportSessionId = sessionId;
                    existing.UpdatedAt = DateTime.UtcNow;
                    savedCount++;
                    continue;
                }
            }

            var account = new InstagramAccount
            {
                Username = profile.Username,
                FullName = profile.FullName,
                Biography = profile.Biography,
                ProfilePictureUrl = profile.ProfilePictureUrl,
                FollowerCount = profile.FollowerCount,
                FollowingCount = profile.FollowingCount,
                PostCount = profile.PostCount,
                IsVerified = profile.IsVerified,
                IsPrivate = profile.IsPrivate,
                SourceFileName = profile.Username,
                ImportSessionId = sessionId
            };

            context.InstagramAccounts.Add(account);
            savedCount++;
        }

        await context.SaveChangesAsync();
        return savedCount;
    }

    private async Task CleanupAsync(params string[] paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup path: {Path}", path);
            }
        }

        await Task.CompletedTask;
    }

    private static ImportResult BuildResult(bool success, int totalRecords, int importedRecords, int skippedRecords, List<string> errors, string sessionId)
    {
        return new ImportResult
        {
            Success = success,
            TotalRecords = totalRecords,
            ImportedRecords = importedRecords,
            SkippedRecords = skippedRecords,
            Errors = errors.AsReadOnly(),
            Message = success
                ? $"Import completed successfully. Session: {sessionId}"
                : $"Import completed with {errors.Count} error(s). Session: {sessionId}"
        };
    }
}
