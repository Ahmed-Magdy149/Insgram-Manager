using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents an Instagram relationship entry parsed from an Instagram data export.
/// Captures the account, display name, and the type of relationship (e.g., followers, following, blocked).
/// </summary>
public class InstagramRelationship : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? ProfileUrl { get; set; }

    public RelationshipType RelationshipType { get; set; }

    public string? SourceFileName { get; set; }

    public string? ImportSessionId { get; set; }
}
