namespace Insgram_Manager.Domain.Enums;

/// <summary>
/// Represents the type of automation action that can be performed.
/// Prepared for Phase 2 automation engine.
/// </summary>
public enum AutomationType
{
    DirectMessage = 0,
    Follow = 1,
    Unfollow = 2,
    Like = 3,
    Comment = 4,
    PostSchedule = 5,
    CancelPendingRequests = 6
}
