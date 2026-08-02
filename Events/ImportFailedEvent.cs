namespace Insgram_Manager.Events;

/// <summary>
/// Published when an import operation fails.
/// No handlers required at this stage — prepared for Phase 2 event-driven architecture.
/// </summary>
public sealed record ImportFailedEvent
{
    public string SessionId { get; init; } = string.Empty;

    public DateTime FailedAt { get; init; } = DateTime.UtcNow;

    public string ErrorMessage { get; init; } = string.Empty;

    public string? ExceptionType { get; init; }
}
