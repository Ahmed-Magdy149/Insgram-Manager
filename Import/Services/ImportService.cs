using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
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
            var allRelationships = new List<InstagramRelationshipModel>();

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
                    allProfiles.AddRange(parseResult.Profiles);
                    allRelationships.AddRange(parseResult.Relationships);
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
            var savedCount = await SaveToDatabaseAsync(allProfiles, allRelationships, sessionId);
            _logger.LogInformation("Saved {Count} accounts and {RelationshipCount} relationships to database",
                savedCount.accountCount, savedCount.relationshipCount);

            // Step 7: Save import record for dashboard history
            await SaveImportRecordAsync(sessionId, request.FileName, statistics, savedCount, errors);

            // Step 8: Cleanup
            await CleanupAsync(tempZipPath, extractionPath);

            // Step 9: Complete
            await _progressService.NotifyCompletedAsync(sessionId, statistics);

            _logger.LogInformation("Import session {SessionId} completed: {Imported}/{Total} records",
                sessionId, statistics.ImportedRecords, statistics.TotalRecords);

            return BuildResult(
                success: errors.Count == 0,
                totalRecords: statistics.TotalRecords,
                importedRecords: savedCount.accountCount,
                skippedRecords: statistics.SkippedRecords,
                errors: errors,
                sessionId: sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import session {SessionId} failed", sessionId);
            errors.Add($"Import failed: {ex.Message}");
            await _progressService.NotifyFailedAsync(sessionId, ex.Message);
            await SaveFailedImportRecordAsync(sessionId, request.FileName, ex.Message);
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

    private async Task<(int accountCount, int relationshipCount)> SaveToDatabaseAsync(
        List<InstagramProfileModel> profiles,
        List<InstagramRelationshipModel> relationships,
        string sessionId)
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

        var relationshipCount = 0;
        var seenRelationships = new HashSet<(string username, RelationshipType type)>(
            new RelationshipKeyComparer());

        foreach (var relationship in relationships)
        {
            if (string.IsNullOrWhiteSpace(relationship.Username))
                continue;

            if (!seenRelationships.Add((relationship.Username, relationship.RelationshipType)))
                continue;

            if (skipDuplicates)
            {
                var existing = await context.InstagramRelationships
                    .FirstOrDefaultAsync(r =>
                        r.Username == relationship.Username &&
                        r.RelationshipType == relationship.RelationshipType);

                if (existing is not null)
                {
                    existing.FullName = relationship.FullName;
                    existing.ProfileUrl = relationship.ProfileUrl;
                    existing.SourceFileName = relationship.ProfileUrl;
                    existing.ImportSessionId = sessionId;
                    existing.UpdatedAt = DateTime.UtcNow;
                    relationshipCount++;
                    continue;
                }
            }

            context.InstagramRelationships.Add(new InstagramRelationship
            {
                Username = relationship.Username,
                FullName = relationship.FullName,
                ProfileUrl = relationship.ProfileUrl,
                RelationshipType = relationship.RelationshipType,
                SourceFileName = relationship.ProfileUrl,
                ImportSessionId = sessionId
            });
            relationshipCount++;
        }

        await context.SaveChangesAsync();
        return (savedCount, relationshipCount);
    }

    private sealed class RelationshipKeyComparer : IEqualityComparer<(string username, RelationshipType type)>
    {
        public bool Equals((string username, RelationshipType type) x, (string username, RelationshipType type) y)
            => string.Equals(x.username, y.username, StringComparison.OrdinalIgnoreCase) && x.type == y.type;

        public int GetHashCode((string username, RelationshipType type) obj)
            => HashCode.Combine(obj.username.ToLowerInvariant(), obj.type);
    }

    private async Task SaveImportRecordAsync(
        string sessionId,
        string fileName,
        ImportStatistics statistics,
        (int accountCount, int relationshipCount) savedCount,
        List<string> errors)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var record = new ImportRecord
        {
            SessionId = sessionId,
            FileName = fileName,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = errors.Count == 0 ? "Completed" : "CompletedWithErrors",
            TotalRecords = statistics.TotalRecords,
            ImportedRecords = savedCount.accountCount,
            SkippedRecords = statistics.SkippedRecords,
            FailedRecords = statistics.FailedRecords,
            AccountCount = savedCount.accountCount,
            RelationshipCount = savedCount.relationshipCount,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };

        context.ImportRecords.Add(record);
        await context.SaveChangesAsync();
    }

    private async Task SaveFailedImportRecordAsync(
        string sessionId,
        string fileName,
        string errorMessage)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var record = new ImportRecord
        {
            SessionId = sessionId,
            FileName = fileName,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = "Failed",
            ErrorMessage = errorMessage
        };

        context.ImportRecords.Add(record);
        await context.SaveChangesAsync();
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
