using Insgram_Manager.Data;
using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Insgram_Manager.Interfaces.Authentication;
using Insgram_Manager.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Insgram_Manager.Services.Authentication;

/// <summary>
/// Handles Instagram-side deletion operations using the existing Playwright browser session.
/// Reuses the persistent browser profile (cookies, localStorage) — never creates a new login.
/// Navigates to each account's Instagram profile and performs the unfollow action.
/// </summary>
public sealed class InstagramDeleteService : IInstagramDeleteService
{
    private readonly IApplicationSessionService _sessionService;
    private readonly IBrowserProfileService _profileService;
    private readonly IInstagramBrowserService _browserService;
    private readonly IPendingRequestService _pendingRequestService;
    private readonly IDbContextFactory<Data.ApplicationDbContext> _contextFactory;
    private readonly BrowserAuthOptions _options;
    private readonly ILogger<InstagramDeleteService> _logger;

    public InstagramDeleteService(
        IDbContextFactory<Data.ApplicationDbContext> contextFactory,
        IApplicationSessionService sessionService,
        IBrowserProfileService profileService,
        IInstagramBrowserService browserService,
        IPendingRequestService pendingRequestService,
        IOptions<BrowserAuthOptions> options,
        ILogger<InstagramDeleteService> logger)
    {
        _contextFactory = contextFactory;
        _sessionService = sessionService;
        _profileService = profileService;
        _browserService = browserService;
        _pendingRequestService = pendingRequestService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeleteResult> DeleteSelectedAsync(
        IReadOnlyList<string> usernames,
        IProgress<DeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (usernames.Count == 0)
            return new DeleteResult { Success = false, Error = "No usernames provided." };

        var accountKey = await _sessionService.GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return new DeleteResult { Success = false, Error = "No active Instagram session." };

        return await ExecuteBatchDeleteAsync(usernames, accountKey, progress, cancellationToken);
    }

    public async Task<DeleteResult> DeleteAllAsync(
        IProgress<DeleteProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var accountKey = await _sessionService.GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return new DeleteResult { Success = false, Error = "No active Instagram session." };

        var pendingRequests = await _pendingRequestService.GetPendingRequestsAsync();
        var usernames = pendingRequests.Select(p => p.Username).ToList();

        if (usernames.Count == 0)
            return new DeleteResult { Success = true, TotalRequested = 0, Succeeded = 0, Failed = 0, Skipped = 0 };

        return await ExecuteBatchDeleteAsync(usernames, accountKey, progress, cancellationToken);
    }

    public async Task<DeleteItemResult> DeleteSingleAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = "Username is required." };

        var accountKey = await _sessionService.GetCurrentAccountKeyAsync(cancellationToken);
        if (accountKey is null)
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = "No active Instagram session." };

        var profileDirectory = _profileService.GetProfileDirectory(accountKey);
        if (profileDirectory is null)
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = "No browser profile found." };

        IPage? page = null;
        try
        {
            page = await _browserService.LaunchPersistentContextAsync(profileDirectory, cancellationToken);
            return await UnfollowUserAsync(page, username, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSingle failed for {Username}", username);
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = ex.Message };
        }
        finally
        {
            if (page is not null)
                await _browserService.CloseAsync();
        }
    }

    private async Task<DeleteResult> ExecuteBatchDeleteAsync(
        IReadOnlyList<string> usernames,
        string accountKey,
        IProgress<DeleteProgress>? progress,
        CancellationToken cancellationToken)
    {
        var profileDirectory = _profileService.GetProfileDirectory(accountKey);
        if (profileDirectory is null)
            return new DeleteResult { Success = false, Error = "No browser profile found.", TotalRequested = usernames.Count };

        var items = new List<DeleteItemResult>(usernames.Count);
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;
        var processed = 0;

        IPage? page = null;
        try
        {
            page = await _browserService.LaunchPersistentContextAsync(profileDirectory, cancellationToken);

            await page.GotoAsync(_options.BaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = _options.NavigationTimeoutSeconds * 1000f
            });

            var isLoggedIn = await _browserService.IsLoggedInAsync(page, cancellationToken);
            if (!isLoggedIn)
            {
                _logger.LogError("Session is not logged in — cannot perform delete operations");
                return new DeleteResult { Success = false, Error = "Session is not logged in.", TotalRequested = usernames.Count };
            }

            _logger.LogInformation("Starting batch delete of {Count} accounts", usernames.Count);

            foreach (var username in usernames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new DeleteProgress
                {
                    Total = usernames.Count,
                    Processed = processed,
                    Succeeded = succeeded,
                    Skipped = skipped,
                    Failed = failed,
                    CurrentUsername = username
                });

                DeleteItemResult item;
                try
                {
                    item = await UnfollowUserAsync(page, username, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete {Username}", username);
                    item = new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = ex.Message };
                }

                items.Add(item);
                processed++;

                switch (item.Status)
                {
                    case DeleteItemStatus.Deleted:
                        succeeded++;
                        break;
                    case DeleteItemStatus.Skipped:
                    case DeleteItemStatus.AlreadyRemoved:
                    case DeleteItemStatus.Private:
                    case DeleteItemStatus.NotFound:
                        skipped++;
                        break;
                    default:
                        failed++;
                        break;
                }

                progress?.Report(new DeleteProgress
                {
                    Total = usernames.Count,
                    Processed = processed,
                    Succeeded = succeeded,
                    Skipped = skipped,
                    Failed = failed,
                    CurrentUsername = username
                });

                await page.WaitForTimeoutAsync(1500);
            }

            _logger.LogInformation(
                "Batch delete complete: {Succeeded} deleted, {Skipped} skipped, {Failed} failed out of {Total}",
                succeeded, skipped, failed, usernames.Count);

            return new DeleteResult
            {
                Success = failed == 0,
                TotalRequested = usernames.Count,
                Succeeded = succeeded,
                Failed = failed,
                Skipped = skipped,
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch delete was cancelled at {Processed}/{Total}", processed, usernames.Count);
            return new DeleteResult
            {
                Success = false,
                TotalRequested = usernames.Count,
                Succeeded = succeeded,
                Failed = failed,
                Skipped = skipped,
                Items = items,
                Error = "Operation was cancelled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch delete failed");
            return new DeleteResult
            {
                Success = false,
                TotalRequested = usernames.Count,
                Succeeded = succeeded,
                Failed = failed,
                Skipped = skipped,
                Items = items,
                Error = ex.Message
            };
        }
        finally
        {
            if (page is not null)
                await _browserService.CloseAsync();
        }
    }

    private async Task<DeleteItemResult> UnfollowUserAsync(IPage page, string username, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var profileUrl = $"{_options.BaseUrl}/{username}/";
        _logger.LogInformation("Navigating to {Url} to unfollow {Username}", profileUrl, username);

        await page.GotoAsync(profileUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = _options.NavigationTimeoutSeconds * 1000f
        });

        await page.WaitForTimeoutAsync(2000);
        cancellationToken.ThrowIfCancellationRequested();

        var currentUrl = page.Url;
        if (currentUrl.Contains("/accounts/login", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Redirected to login page — session may have expired");
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Failed, Error = "Session expired." };
        }

        if (currentUrl.Equals($"{_options.BaseUrl}/", StringComparison.OrdinalIgnoreCase) ||
            currentUrl.Contains("?", StringComparison.OrdinalIgnoreCase) && !currentUrl.Contains($"/{username}", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("User {Username} not found — profile redirected", username);
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.NotFound };
        }

        var privateBadge = await page.QuerySelectorAsync("text='This Account is Private'");
        if (privateBadge is not null && await privateBadge.IsVisibleAsync())
        {
            _logger.LogInformation("User {Username} is private — checking follow state", username);
        }

        var followingButtonSelectors = new[]
        {
            "button:has-text('Following')",
            "button:has-text('Requested')",
            "div[role='button']:has-text('Following')",
            "div[role='button']:has-text('Requested')",
            "button[type='button']:has-text('Following')",
            "button[type='button']:has-text('Requested')"
        };

        IElementHandle? actionButton = null;
        foreach (var selector in followingButtonSelectors)
        {
            try
            {
                actionButton = await page.QuerySelectorAsync(selector);
                if (actionButton is not null && await actionButton.IsVisibleAsync())
                    break;
                actionButton = null;
            }
            catch
            {
            }
        }

        if (actionButton is null)
        {
            var followButtonSelectors = new[]
            {
                "button:has-text('Follow')",
                "div[role='button']:has-text('Follow')"
            };

            foreach (var selector in followButtonSelectors)
            {
                try
                {
                    var followBtn = await page.QuerySelectorAsync(selector);
                    if (followBtn is not null && await followBtn.IsVisibleAsync())
                    {
                        _logger.LogInformation("User {Username} is not followed — skipping", username);
                        return new DeleteItemResult { Username = username, Status = DeleteItemStatus.AlreadyRemoved };
                    }
                }
                catch
                {
                }
            }

            _logger.LogInformation("No Following/Requested/Follow button found for {Username} — may be already removed", username);
            return new DeleteItemResult { Username = username, Status = DeleteItemStatus.AlreadyRemoved };
        }

        var buttonText = await actionButton.TextContentAsync() ?? string.Empty;

        if (buttonText.Contains("Requested", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Found 'Requested' button for {Username} — clicking to cancel request", username);
        }
        else if (buttonText.Contains("Following", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Found 'Following' button for {Username} — clicking to unfollow", username);
        }

        await actionButton.ClickAsync();
        await page.WaitForTimeoutAsync(1000);

        var confirmSelectors = new[]
        {
            "button:has-text('Unfollow')",
            "div[role='button']:has-text('Unfollow')"
        };

        foreach (var selector in confirmSelectors)
        {
            try
            {
                var confirmBtn = await page.QuerySelectorAsync(selector);
                if (confirmBtn is not null && await confirmBtn.IsVisibleAsync())
                {
                    await confirmBtn.ClickAsync();
                    await page.WaitForTimeoutAsync(1500);
                    break;
                }
            }
            catch
            {
            }
        }

        try
        {
            await using var context = await GetDbContextAsync();
            var relationship = await context.InstagramRelationships
                .FirstOrDefaultAsync(r => r.Username == username && r.RelationshipType == RelationshipType.PendingRequests, cancellationToken);

            if (relationship is not null)
            {
                context.InstagramRelationships.Remove(relationship);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Removed {Username} from database", username);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove {Username} from database", username);
        }

        _logger.LogInformation("Successfully unfollowed {Username}", username);
        return new DeleteItemResult { Username = username, Status = DeleteItemStatus.Deleted };
    }

    private async Task<Data.ApplicationDbContext> GetDbContextAsync()
    {
        return await _contextFactory.CreateDbContextAsync();
    }
}
