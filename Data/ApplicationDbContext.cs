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

        base.OnModelCreating(modelBuilder);
    }
}
