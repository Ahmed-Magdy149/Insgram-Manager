namespace Insgram_Manager.Events;

/// <summary>
/// Published when an import operation starts.
/// No handlers required at this stage — prepared for Phase 2 event-driven architecture.
/// </summary>
public sealed record ImportStartedEvent
{
    public string SessionId { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
}
