namespace Insgram_Manager.Domain.Entities;

/// <summary>
/// Represents a locally stored Instagram communication session.
/// Sensitive values are encrypted before persistence.
/// </summary>
public class InstagramSession : BaseEntity
{
    public string AccountKey { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string? ProtectedAccessToken { get; set; }

    public string? ProtectedCookieHeader { get; set; }

    public string? ProtectedCsrfToken { get; set; }

    public bool IsAuthenticated { get; set; }

    public DateTime? LastAuthenticatedAt { get; set; }

    public DateTime? LastRefreshedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Notes { get; set; }
}
