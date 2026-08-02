using Insgram_Manager.Components.Shared;
using Insgram_Manager.Domain.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Dashboard page.
/// Loads real import statistics from the database and displays them.
/// </summary>
public class DashboardPageBase : BaseComponent
{
    [Inject]
    private IDbContextFactory<Data.ApplicationDbContext> DbContextFactory { get; set; } = default!;

    protected int ImportedAccountCount { get; set; }
    protected int VerifiedAccountCount { get; set; }
    protected int PrivateAccountCount { get; set; }
    protected long TotalFollowers { get; set; }
    protected IReadOnlyList<InstagramAccount> RecentAccounts { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadStatsAsync();
    }

    protected async Task LoadStatsAsync()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        ImportedAccountCount = await context.InstagramAccounts.CountAsync();
        VerifiedAccountCount = await context.InstagramAccounts.CountAsync(a => a.IsVerified);
        PrivateAccountCount = await context.InstagramAccounts.CountAsync(a => a.IsPrivate);
        TotalFollowers = await context.InstagramAccounts.SumAsync(a => (long)a.FollowerCount);

        RecentAccounts = await context.InstagramAccounts
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .AsNoTracking()
            .ToListAsync();

        StateHasChanged();
    }

    protected void NavigateToImport()
    {
        NavigationManager.NavigateTo("/import");
    }
}
