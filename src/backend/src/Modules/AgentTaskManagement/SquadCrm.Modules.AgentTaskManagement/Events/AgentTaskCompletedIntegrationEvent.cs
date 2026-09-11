using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a completed agent
/// task (CRM-143, ADR-005). Producer side only — no dispatcher/consumer reads
/// it yet (CRM-144 Agent Task Reminders is the future consumer).
/// </summary>
internal sealed record AgentTaskCompletedIntegrationEvent(
    Guid EventId,
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>Stable, versioned contract name — durable data (ADR-005).</summary>
    public const string ContractName = "agent-task-management.agent-task-completed.v1";

    public string Type => ContractName;
}
