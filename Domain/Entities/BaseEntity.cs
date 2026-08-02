namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Abstract base class for all domain entities.
/// Provides common audit fields: Id, CreatedAt, UpdatedAt.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
