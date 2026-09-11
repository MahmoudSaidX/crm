using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Raised inside this module when a completed task is reopened (CRM-143
/// Business Rule: reopen is an explicitly supported workflow, not a deletion).
/// Translated into <see cref="AgentTaskReopenedIntegrationEvent"/> by
/// <see cref="Persistence.AgentTaskManagementOutboxInterceptor"/> before it
/// leaves the module (ADR-005).
/// </summary>
public sealed record AgentTaskReopenedDomainEvent(
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
