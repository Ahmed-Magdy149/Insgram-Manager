using System.Net;

namespace Insgram_Manager.Communication.Models;

/// <summary>
/// Represents a locally managed Instagram communication session.
/// The access token/cookie/CSRF material is protected at rest by the session service.
/// </summary>
public sealed record InstagramSessionState
{
    public string AccountKey { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string? AccessToken { get; init; }

    public string? CookieHeader { get; init; }

    public string? CsrfToken { get; init; }

    public bool IsAuthenticated { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime? LastAuthenticatedAt { get; init; }

    public DateTime? LastRefreshedAt { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public string? Notes { get; init; }
}

/// <summary>
/// Represents a tracked Instagram communication request/response pair.
/// Stored locally so the UI can present durable interaction history.
/// </summary>
public sealed record InstagramInteractionRecord
{
    public string InteractionId { get; init; } = Guid.NewGuid().ToString("N");

    public string AccountKey { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Method { get; init; } = "GET";

    public string Endpoint { get; init; } = string.Empty;

    public int? StatusCode { get; init; }

    public bool Success { get; init; }

    public string? Summary { get; init; }

    public string? Error { get; init; }

    public string? RequestBody { get; init; }

    public string? ResponseBody { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public TimeSpan? Duration { get; init; }
}

/// <summary>
/// Serialized store containing all locally managed communication sessions and history.
/// Persisted via settings and protected by Data Protection.
/// </summary>
public sealed class InstagramCommunicationStore
{
    public List<InstagramSessionState> Sessions { get; set; } = [];

    public List<InstagramInteractionRecord> Interactions { get; set; } = [];
}

/// <summary>
/// Lightweight response projection returned by the request engine.
/// </summary>
public sealed record InstagramApiResponse
{
    public HttpStatusCode? StatusCode { get; init; }

    public bool Success { get; init; }

    public string Method { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string? Content { get; init; }

    public string? Error { get; init; }

    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Aggregated statistics for the communication page.
/// </summary>
public sealed record InstagramCommunicationStatsDto
{
    public int TotalSessions { get; init; }

    public int AuthenticatedSessions { get; init; }

    public int ExpiringSoonSessions { get; init; }

    public int TotalInteractions { get; init; }

    public int FailedInteractions { get; init; }

    public DateTime? LastActivityAt { get; init; }
}
