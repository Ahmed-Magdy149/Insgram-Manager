using Insgram_Manager.Domain.Enums;
using Insgram_Manager.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Insgram_Manager.Services;

/// <summary>
/// Dashboard service that aggregates statistics from the database.
/// Reads account counts, relationship breakdowns, and import history.
/// All queries are async and use AsNoTracking for read-only performance.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<Data.ApplicationDbContext> _contextFactory;
    private readonly ILogger<DashboardService> _logger;

    private static readonly Dictionary<RelationshipType, (string Label, string Color)> RelationshipMeta = new()
    {
        [RelationshipType.Followers] = ("Followers", "blue"),
        [RelationshipType.Following] = ("Following", "green"),
        [RelationshipType.PendingRequests] = ("Pending Requests", "orange"),
        [RelationshipType.Blocked] = ("Blocked", "red"),
        [RelationshipType.RecentFollowRequests] = ("Recent Follow Requests", "purple"),
        [RelationshipType.RemovedSuggestions] = ("Removed Suggestions", "default")
    };

    public DashboardService(
        IDbContextFactory<Data.ApplicationDbContext> contextFactory,
        ILogger<DashboardService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var accounts = context.InstagramAccounts.AsNoTracking();
        var relationships = context.InstagramRelationships.AsNoTracking();

        var importedAccountCount = await accounts.CountAsync();
        var verifiedAccountCount = await accounts.CountAsync(a => a.IsVerified);
        var privateAccountCount = await accounts.CountAsync(a => a.IsPrivate);
        var totalFollowers = await accounts.SumAsync(a => (long)a.FollowerCount);
        var totalFollowing = await accounts.SumAsync(a => (long)a.FollowingCount);

        var totalRelationships = await relationships.CountAsync();
        var followersCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.Followers);
        var followingCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.Following);
        var pendingRequestsCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.PendingRequests);
        var blockedCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.Blocked);
        var recentFollowRequestsCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.RecentFollowRequests);
        var removedSuggestionsCount = await relationships.CountAsync(r => r.RelationshipType == RelationshipType.RemovedSuggestions);

        var totalImports = await context.ImportRecords.AsNoTracking().CountAsync();

        return new DashboardStatsDto
        {
            ImportedAccountCount = importedAccountCount,
            VerifiedAccountCount = verifiedAccountCount,
            PrivateAccountCount = privateAccountCount,
            TotalFollowers = totalFollowers,
            TotalFollowing = totalFollowing,
            TotalRelationships = totalRelationships,
            FollowersCount = followersCount,
            FollowingCount = followingCount,
            PendingRequestsCount = pendingRequestsCount,
            BlockedCount = blockedCount,
            RecentFollowRequestsCount = recentFollowRequestsCount,
            RemovedSuggestionsCount = removedSuggestionsCount,
            TotalImports = totalImports
        };
    }

    public async Task<IReadOnlyList<RelationshipBreakdownItem>> GetRelationshipBreakdownAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var counts = await context.InstagramRelationships
            .AsNoTracking()
            .GroupBy(r => r.RelationshipType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        var total = counts.Values.Sum();
        var items = new List<RelationshipBreakdownItem>();

        foreach (var (type, (label, color)) in RelationshipMeta)
        {
            var count = counts.GetValueOrDefault(type, 0);
            var percentage = total > 0 ? Math.Round((double)count / total * 100, 1) : 0;

            items.Add(new RelationshipBreakdownItem
            {
                Type = type,
                Label = label,
                Count = count,
                Color = color,
                Percentage = percentage
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<ImportHistoryItem>> GetImportHistoryAsync(int maxRecords = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var records = await context.ImportRecords
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(maxRecords)
            .Select(r => new ImportHistoryItem
            {
                SessionId = r.SessionId,
                FileName = r.FileName,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                Status = r.Status,
                TotalRecords = r.TotalRecords,
                ImportedRecords = r.ImportedRecords,
                AccountCount = r.AccountCount,
                RelationshipCount = r.RelationshipCount,
                ErrorMessage = r.ErrorMessage
            })
            .ToListAsync();

        return records;
    }

    public async Task<ImportHistoryItem?> GetLastImportAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var record = await context.ImportRecords
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (record is null)
            return null;

        return new ImportHistoryItem
        {
            SessionId = record.SessionId,
            FileName = record.FileName,
            StartedAt = record.StartedAt,
            CompletedAt = record.CompletedAt,
            Status = record.Status,
            TotalRecords = record.TotalRecords,
            ImportedRecords = record.ImportedRecords,
            AccountCount = record.AccountCount,
            RelationshipCount = record.RelationshipCount,
            ErrorMessage = record.ErrorMessage
        };
    }
}
