using Insgram_Manager.Import.Models;

namespace Insgram_Manager.Events;

/// <summary>
/// Published when an import operation completes successfully.
/// No handlers required at this stage — prepared for Phase 2 event-driven architecture.
/// </summary>
public sealed record ImportCompletedEvent
{
    public string SessionId { get; init; } = string.Empty;

    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;

    public ImportStatistics Statistics { get; init; } = new();
}
