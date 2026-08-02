namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents an Instagram account parsed from an Instagram data export.
/// Stores profile metadata and engagement statistics.
/// </summary>
public class InstagramAccount : BaseEntity
{
    public string Username { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? Biography { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public int FollowerCount { get; set; }

    public int FollowingCount { get; set; }

    public int PostCount { get; set; }

    public bool IsVerified { get; set; }

    public bool IsPrivate { get; set; }

    public string? SourceFileName { get; set; }

    public string? ImportSessionId { get; set; }
}
