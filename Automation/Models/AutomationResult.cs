using Insgram_Manager.Domain.Enums;

namespace Insgram_Manager.Automation.Models;

/// <summary>
/// Represents the result of an automation job execution.
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public sealed class AutomationResult
{
    public bool Success { get; init; }

    public string JobId { get; init; } = string.Empty;

    public AutomationType Type { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
}
