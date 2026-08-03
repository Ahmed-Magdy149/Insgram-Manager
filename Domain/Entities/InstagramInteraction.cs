namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents a persisted Instagram interaction history entry.
/// Used for communication tracking, debugging, and auditability.
/// </summary>
public class InstagramInteraction : BaseEntity
{
    public string AccountKey { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Endpoint { get; set; } = string.Empty;

    public int? StatusCode { get; set; }

    public bool Success { get; set; }

    public string? Summary { get; set; }

    public string? Error { get; set; }

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public TimeSpan? Duration { get; set; }
}
