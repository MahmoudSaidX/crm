using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Raised inside this module when an <see cref="Persistence.AgentTask"/> is
/// created (CRM-143). Never crosses the module boundary directly — this
/// module's own <see cref="Persistence.AgentTaskManagementOutboxInterceptor"/>
/// translates it into the explicit <see cref="AgentTaskCreatedIntegrationEvent"/>
/// contract before it leaves the module (ADR-005).
/// </summary>
public sealed record AgentTaskCreatedDomainEvent(
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
