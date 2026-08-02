using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents a parsed Instagram relationship data model.
/// </summary>
public sealed class InstagramRelationshipModel
{
    public string Username { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? ProfileUrl { get; set; }

    public RelationshipType RelationshipType { get; set; }
}
