using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a due task
/// reminder (CRM-144, ADR-005) — the "durable reminder event for in-app/
/// external notification delivery" the story's Acceptance Criteria requires.
/// Producer only: CRM-155 (In-App Alerts &amp; Notifications Center) and
/// CRM-156 (Generate Ticket, SLA &amp; Task Notifications) are the future
/// consumers and are blocked by this story, so no dispatcher or delivery
/// channel is built here (YAGNI).
/// <para>
/// <see cref="EventId"/> is the task's <c>ReminderEventId</c>, passed through
/// verbatim rather than newly generated. The outbox interceptor uses it as
/// the outbox row's primary key, so a retried sweep of the same occurrence
/// cannot produce a second reminder — and a consumer can additionally
/// de-duplicate on it.
/// </para>
/// <para>
/// The payload carries only what a notification needs to render and link.
/// It grants no access: a deep link into the task/ticket/customer is
/// re-authorized by the owning module when followed (BR).
/// </para>
/// </summary>
internal sealed record AgentTaskReminderDueIntegrationEvent(
    Guid EventId,
    Guid TaskId,
    string Title,
    Guid OwnerUserId,
    DateTimeOffset ReminderAtUtc,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "agent-task-management.agent-task-reminder-due.v1";

    public string Type => ContractName;
}
