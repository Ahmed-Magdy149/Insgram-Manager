namespace Insgram_Manager.Domain.Enums;

/// <summary>
/// Represents the type of Instagram relationship list that can be imported and explored.
/// </summary>
public enum RelationshipType
{
    Followers = 0,
    Following = 1,
    PendingRequests = 2,
    Blocked = 3,
    RecentFollowRequests = 4,
    RemovedSuggestions = 5
}
