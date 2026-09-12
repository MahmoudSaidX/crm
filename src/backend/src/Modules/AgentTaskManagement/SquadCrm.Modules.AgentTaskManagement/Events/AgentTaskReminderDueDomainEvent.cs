using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Raised inside this module when a scheduled task reminder falls due
/// (CRM-144). Never crosses the module boundary directly — this module's own
/// <see cref="Persistence.AgentTaskManagementOutboxInterceptor"/> translates
/// it into the explicit <see cref="AgentTaskReminderDueIntegrationEvent"/>
/// contract before it leaves the module (ADR-005).
/// <para>
/// <paramref name="ReminderEventId"/> is the occurrence identity carried over
/// from <c>AgentTask.ReminderEventId</c> — it is NOT regenerated here,
/// because it is what makes a retried sweep idempotent (BR).
/// </para>
/// </summary>
public sealed record AgentTaskReminderDueDomainEvent(
    Guid ReminderEventId,
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset ReminderAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
