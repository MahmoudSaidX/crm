using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a created agent
/// task (CRM-143, ADR-005). Only the producer side is built by this story —
/// the <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the <see cref="Persistence.AgentTask"/> insert; no
/// dispatcher/consumer reads it yet (YAGNI — CRM-144 Agent Task Reminders is
/// the future consumer).
/// </summary>
internal sealed record AgentTaskCreatedIntegrationEvent(
    Guid EventId,
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "agent-task-management.agent-task-created.v1";

    public string Type => ContractName;
}
