namespace Insgram_Manager.Import.Models;

/// <summary>
/// Represents a parsed Instagram profile data model.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class InstagramProfileModel
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
}
