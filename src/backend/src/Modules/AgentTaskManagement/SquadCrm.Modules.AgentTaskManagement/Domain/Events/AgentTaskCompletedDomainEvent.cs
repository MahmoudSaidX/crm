using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Domain.Events;

/// <summary>
/// Raised inside this module when a task is completed (CRM-143). Translated
/// into <see cref="AgentTaskCompletedIntegrationEvent"/> by
/// <see cref="AgentTaskManagementOutboxInterceptor"/> before it
/// leaves the module (ADR-005).
/// </summary>
public sealed record AgentTaskCompletedDomainEvent(
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
