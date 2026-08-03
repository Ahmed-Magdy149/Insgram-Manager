using Insgram_Manager.Communication.Models;
using Insgram_Manager.Data;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services;

/// <summary>
/// Manages the cancellation of pending Instagram follow requests.
/// Reads pending requests from the database, sends unfollow requests via the
/// Instagram request service, and removes successfully cancelled entries.
/// </summary>
public sealed class PendingRequestService : IPendingRequestService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IInstagramRequestService _requestService;
    private readonly IInstagramSessionService _sessionService;
    private readonly IInstagramRateLimiter _rateLimiter;
    private readonly IInstagramCommunicationProgressService _progressService;
    private readonly ILogger<PendingRequestService> _logger;

    public PendingRequestService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IInstagramRequestService requestService,
        IInstagramSessionService sessionService,
        IInstagramRateLimiter rateLimiter,
        IInstagramCommunicationProgressService progressService,
        ILogger<PendingRequestService> logger)
    {
        _contextFactory = contextFactory;
        _requestService = requestService;
        _sessionService = sessionService;
        _rateLimiter = rateLimiter;
        _progressService = progressService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PendingRequestItem>> GetPendingRequestsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var items = await context.InstagramRelationships
            .AsNoTracking()
            .Where(r => r.RelationshipType == RelationshipType.PendingRequests)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PendingRequestItem
            {
                Id = r.Id,
                Username = r.Username,
                FullName = r.FullName,
                ProfileUrl = r.ProfileUrl,
                ImportedAt = r.CreatedAt
            })
            .ToListAsync();

        return items;
    }

    public async Task<PendingRequestCancelResult> CancelAsync(string username, string accountKey)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new PendingRequestCancelResult { Success = false, Error = "Username is required." };

        var isValid = await _sessionService.ValidateSessionAsync(accountKey);
        if (!isValid)
        {
            _logger.LogWarning("Cannot cancel pending request for {Username}: session {AccountKey} is not valid.", username, accountKey);
            return new PendingRequestCancelResult
            {
                Username = username,
                Success = false,
                Error = "Instagram session is not authenticated or has expired."
            };
        }

        var endpoint = $"/api/v1/web/friendships/{username}/unfollow/";
        var response = await _requestService.SendAsync(accountKey, HttpMethod.Post, endpoint);

        var result = new PendingRequestCancelResult
        {
            Username = username,
            Success = response.Success,
            Error = response.Success ? null : (response.Error ?? $"HTTP {(int?)response.StatusCode ?? 0}"),
            StatusCode = (int?)response.StatusCode,
            CompletedAt = DateTime.UtcNow
        };

        if (result.Success)
        {
            await RemoveFromDatabaseAsync(username);
            _logger.LogInformation("Successfully cancelled pending follow request for {Username}", username);
        }
        else
        {
            _logger.LogWarning("Failed to cancel pending follow request for {Username}: {Error}", username, result.Error);
        }

        return result;
    }

    public async Task<IReadOnlyList<PendingRequestCancelResult>> CancelBatchAsync(
        IReadOnlyList<string> usernames,
        string accountKey,
        IProgress<PendingRequestBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (usernames.Count == 0)
            return [];

        var results = new List<PendingRequestCancelResult>(usernames.Count);
        var succeeded = 0;
        var failed = 0;

        for (var i = 0; i < usernames.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = usernames[i];
            var batchProgress = new PendingRequestBatchProgress
            {
                Total = usernames.Count,
                Processed = i,
                Succeeded = succeeded,
                Failed = failed,
                CurrentUsername = username
            };

            progress?.Report(batchProgress);
            await _progressService.NotifyPendingRequestProgressAsync(batchProgress);

            var result = await CancelAsync(username, accountKey);
            results.Add(result);

            if (result.Success)
                succeeded++;
            else
                failed++;
        }

        var finalProgress = new PendingRequestBatchProgress
        {
            Total = usernames.Count,
            Processed = usernames.Count,
            Succeeded = succeeded,
            Failed = failed,
            CurrentUsername = string.Empty
        };

        progress?.Report(finalProgress);
        await _progressService.NotifyPendingRequestProgressAsync(finalProgress);
        await _progressService.NotifyPendingRequestCompletedAsync(results);

        _logger.LogInformation(
            "Batch cancellation complete: {Total} processed, {Succeeded} succeeded, {Failed} failed",
            usernames.Count, succeeded, failed);

        return results;
    }

    public async Task<IReadOnlyList<PendingRequestCancelResult>> RetryFailedAsync(
        IReadOnlyList<PendingRequestCancelResult> failedResults,
        string accountKey,
        IProgress<PendingRequestBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var failedUsernames = failedResults
            .Where(r => !r.Success)
            .Select(r => r.Username)
            .Distinct()
            .ToList();

        if (failedUsernames.Count == 0)
            return [];

        _logger.LogInformation("Retrying {Count} failed cancellations", failedUsernames.Count);
        return await CancelBatchAsync(failedUsernames, accountKey, progress, cancellationToken);
    }

    public async Task<bool> RemoveFromDatabaseAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var entries = await context.InstagramRelationships
            .Where(r => r.Username == username && r.RelationshipType == RelationshipType.PendingRequests)
            .ToListAsync();

        if (entries.Count == 0)
            return false;

        context.InstagramRelationships.RemoveRange(entries);
        await context.SaveChangesAsync();

        _logger.LogDebug("Removed {Count} pending request entries for {Username}", entries.Count, username);
        return true;
    }
}
