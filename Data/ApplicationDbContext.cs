using Insgram_Manager.Domain.Entities;
using Insgram_Manager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Insgram_Manager.Data;

/// <summary>
/// EF Core DbContext for the Insgram Manager application.
/// Uses SQLite as the backing store.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Setting> Settings => Set<Setting>();

    public DbSet<InstagramAccount> InstagramAccounts => Set<InstagramAccount>();

    public DbSet<InstagramRelationship> InstagramRelationships => Set<InstagramRelationship>();

    public DbSet<ImportRecord> ImportRecords => Set<ImportRecord>();

    public DbSet<AutomationJob> AutomationJobs => Set<AutomationJob>();

    public DbSet<InstagramSession> InstagramSessions => Set<InstagramSession>();

    public DbSet<InstagramInteraction> InstagramInteractions => Set<InstagramInteraction>();

    public DbSet<InstagramAuthSession> InstagramAuthSessions => Set<InstagramAuthSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Value).HasMaxLength(2048);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<InstagramAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(512);
            entity.Property(e => e.Biography).HasMaxLength(4096);
            entity.Property(e => e.ProfilePictureUrl).HasMaxLength(2048);
            entity.Property(e => e.SourceFileName).HasMaxLength(512);
            entity.Property(e => e.ImportSessionId).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<InstagramRelationship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username);
            entity.HasIndex(e => e.RelationshipType);
            entity.HasIndex(e => new { e.Username, e.RelationshipType });
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(512);
            entity.Property(e => e.ProfileUrl).HasMaxLength(2048);
            entity.Property(e => e.SourceFileName).HasMaxLength(512);
            entity.Property(e => e.ImportSessionId).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<ImportRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4096);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<AutomationJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.JobId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ParametersJson).HasMaxLength(8192);
            entity.Property(e => e.ErrorMessage).HasMaxLength(4096);
            entity.Property(e => e.ResultMessage).HasMaxLength(2048);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<InstagramSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountKey).IsUnique();
            entity.Property(e => e.AccountKey).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ProtectedAccessToken).HasMaxLength(8192);
            entity.Property(e => e.ProtectedCookieHeader).HasMaxLength(8192);
            entity.Property(e => e.ProtectedCsrfToken).HasMaxLength(1024);
            entity.Property(e => e.Notes).HasMaxLength(2048);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<InstagramInteraction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountKey);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.AccountKey).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Method).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Summary).HasMaxLength(2048);
            entity.Property(e => e.Error).HasMaxLength(4096);
            entity.Property(e => e.RequestBody).HasMaxLength(8192);
            entity.Property(e => e.ResponseBody).HasMaxLength(8192);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<InstagramAuthSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountKey).IsUnique();
            entity.Property(e => e.AccountKey).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(512);
            entity.Property(e => e.ProfilePictureUrl).HasMaxLength(2048);
            entity.Property(e => e.ProfileDirectory).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        base.OnModelCreating(modelBuilder);
    }
}
