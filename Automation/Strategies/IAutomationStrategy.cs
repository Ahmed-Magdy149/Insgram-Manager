using Insgram_Manager.Automation.Models;

namespace Insgram_Manager.Automation.Strategies;

/// <summary>
/// Abstraction for automation strategies that define how a specific
/// automation type is executed (e.g. DM, Follow, Like).
/// Prepared for Phase 2 — no implementation yet.
/// </summary>
public interface IAutomationStrategy
{
    Domain.Enums.AutomationType SupportedType { get; }

    Task<AutomationResult> ExecuteAsync(QueuedJob job, CancellationToken cancellationToken = default);
}
