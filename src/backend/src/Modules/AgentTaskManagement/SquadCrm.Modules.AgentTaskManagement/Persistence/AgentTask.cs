using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.AgentTaskManagement.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

public enum AgentTaskStatus
{
    Open,

    /// <summary>
    /// Terminal for normal display purposes only — reopening (BR) is an
    /// explicitly supported transition back to <see cref="Open"/>, so this is
    /// not a one-way transition the way it might first appear.
    /// </summary>
    Completed,
}

/// <summary>
/// Lifecycle of a task's single optional reminder (CRM-144 Fields
/// Dictionary). Deliberately separate from <see cref="AgentTaskStatus"/>: a
/// reminder is cancelled or fired independently of whether its task is open.
/// </summary>
public enum AgentTaskReminderStatus
{
    /// <summary>No reminder is set — the default, and the state after clearing one.</summary>
    None,

    /// <summary>A future reminder is set and awaiting the due-reminder sweep.</summary>
    Scheduled,

    /// <summary>The reminder fell due and its durable reminder event was written to the outbox.</summary>
    Triggered,

    /// <summary>The task was completed while a reminder was still scheduled (AC: no future reminders).</summary>
    Cancelled,
}

/// <summary>
/// A simple personal/support follow-up task owned by one agent (CRM-143).
/// Deliberately narrow per the story's Deadline Acceptance Override: no
/// delegation, no recurrence, no dependencies, no task engine.
/// <para>
/// <see cref="TicketId"/>/<see cref="CustomerId"/> are plain optional
/// existence-checked references (via <c>ITicketExistsLookup</c>/
/// <c>ICustomerExistsLookup</c> at write time) — linking a task to either
/// never grants access to that resource (Business Rule); each is
/// independently authorized by its own owning module when navigated to.
/// </para>
/// </summary>
public sealed class AgentTask : HasDomainEvents
{
    public Guid Id { get; private set; }
    public required string Title { get; set; }
    public string? Details { get; set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? TicketId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public AgentTaskStatus Status { get; private set; } = AgentTaskStatus.Open;
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The single optional reminder instant, always UTC (CRM-144 scope
    /// override: one reminder per task, no recurrence). User-facing timezone
    /// conversion happens in the UI; persistence and scheduling are UTC-only.
    /// </summary>
    public DateTimeOffset? ReminderAtUtc { get; private set; }

    public AgentTaskReminderStatus ReminderStatus { get; private set; } = AgentTaskReminderStatus.None;

    /// <summary>
    /// Identity of the CURRENT scheduled reminder occurrence, and the whole
    /// idempotency/supersession mechanism (BR "reminder event identity must be
    /// stable enough to prevent duplicate notifications on retries").
    /// <para>
    /// When the reminder fires this id becomes the reminder integration
    /// event's <c>EventId</c>, which the outbox interceptor uses verbatim as
    /// the <see cref="OutboxMessage"/> primary key. A retried sweep therefore
    /// cannot insert a second row for the same occurrence, and rescheduling
    /// mints a fresh id so a superseded occurrence can never be confused with
    /// the new one.
    /// </para>
    /// </summary>
    public Guid? ReminderEventId { get; private set; }

    public DateTimeOffset? ReminderTriggeredAtUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token, same rationale as <c>Ticket.Version</c>:
    /// an explicit column rather than the Postgres <c>xmin</c> system column.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>
    /// The only way to construct a task outside EF materialization — ensures
    /// <see cref="AgentTaskCreatedDomainEvent"/> is always raised alongside a
    /// new task, never forgotten at a second call site.
    /// </summary>
    public static AgentTask Create(
        Guid id,
        string title,
        string? details,
        Guid ownerUserId,
        Guid? ticketId,
        Guid? customerId,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset createdAtUtc)
    {
        AgentTask task = new()
        {
            Id = id,
            Title = title,
            Details = details,
            OwnerUserId = ownerUserId,
            TicketId = ticketId,
            CustomerId = customerId,
            DueAtUtc = dueAtUtc,
            Status = AgentTaskStatus.Open,
            CreatedAtUtc = createdAtUtc,
        };
        task.AddDomainEvent(new AgentTaskCreatedDomainEvent(id, title, ownerUserId, dueAtUtc, createdAtUtc));
        return task;
    }

    /// <summary>
    /// Applies a field edit (title/details/due date/linked ticket or
    /// customer). Ownership, the version check and link-existence validation
    /// are enforced by <c>AgentTaskService</c> before this is called — this
    /// method only applies an already-validated change. No domain event: the
    /// story's Fields Dictionary requires durable events only for
    /// create/complete/reopen (AC).
    /// </summary>
    public void ApplyEdit(
        string title,
        string? details,
        Guid? ticketId,
        Guid? customerId,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset changedAtUtc)
    {
        Title = title;
        Details = details;
        TicketId = ticketId;
        CustomerId = customerId;
        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = changedAtUtc;
        Version++;
    }

    /// <summary>
    /// Applies completion (CRM-143 AC "completing a task preserves history
    /// rather than deleting it" — the row is updated in place, never removed).
    /// Eligibility (open, owner, version) is enforced by
    /// <c>AgentTaskService</c> before this is called.
    /// </summary>
    public void Complete(DateTimeOffset completedAtUtc)
    {
        Status = AgentTaskStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        UpdatedAtUtc = completedAtUtc;
        Version++;

        // AC (CRM-144): a completed task must not generate future reminders.
        // Dropping the event id as well means the superseded occurrence can
        // never be resurrected by a late sweep.
        if (ReminderStatus == AgentTaskReminderStatus.Scheduled)
        {
            ReminderStatus = AgentTaskReminderStatus.Cancelled;
            ReminderEventId = null;
        }

        AddDomainEvent(new AgentTaskCompletedDomainEvent(Id, Title, OwnerUserId, completedAtUtc, completedAtUtc));
    }

    /// <summary>
    /// Applies the explicitly supported reopen workflow (BR: completed tasks
    /// are immutable in completion metadata except through this transition).
    /// Eligibility (completed, owner, version) is enforced by
    /// <c>AgentTaskService</c> before this is called.
    /// <para>
    /// A reminder cancelled by completion is deliberately NOT resurrected
    /// here (CRM-144): its instant is virtually always in the past by now,
    /// so restoring it would fire an immediate, useless alert. The owner
    /// sets a new one.
    /// </para>
    /// </summary>
    public void Reopen(DateTimeOffset reopenedAtUtc)
    {
        Status = AgentTaskStatus.Open;
        CompletedAtUtc = null;
        UpdatedAtUtc = reopenedAtUtc;
        Version++;

        AddDomainEvent(new AgentTaskReopenedDomainEvent(Id, Title, OwnerUserId, reopenedAtUtc));
    }

    /// <summary>
    /// Sets or reschedules the task's single reminder (CRM-144 AC). Minting a
    /// fresh <see cref="ReminderEventId"/> is what supersedes any previous
    /// occurrence — the old id is discarded, so the replaced reminder can
    /// never also fire ("without duplicate alerts", AC).
    /// <para>
    /// Eligibility (open task, owner, version, future instant) is enforced by
    /// <c>AgentTaskService</c> before this is called.
    /// </para>
    /// </summary>
    public void SetReminder(DateTimeOffset reminderAtUtc, DateTimeOffset changedAtUtc)
    {
        ReminderAtUtc = reminderAtUtc.ToUniversalTime();
        ReminderStatus = AgentTaskReminderStatus.Scheduled;
        ReminderEventId = Guid.NewGuid();
        ReminderTriggeredAtUtc = null;
        UpdatedAtUtc = changedAtUtc;
        Version++;
    }

    /// <summary>
    /// Clears the reminder (CRM-144 AC "set, update or clear"; the Fields
    /// Dictionary defines a null <c>ReminderAtUtc</c> as cleared). Eligibility
    /// is enforced by <c>AgentTaskService</c> before this is called.
    /// </summary>
    public void ClearReminder(DateTimeOffset changedAtUtc)
    {
        ReminderAtUtc = null;
        ReminderStatus = AgentTaskReminderStatus.None;
        ReminderEventId = null;
        ReminderTriggeredAtUtc = null;
        UpdatedAtUtc = changedAtUtc;
        Version++;
    }

    /// <summary>
    /// Fires a due reminder: <c>Scheduled -&gt; Triggered</c>, raising the
    /// durable reminder domain event that the outbox interceptor translates
    /// and persists in the SAME transaction as this transition (AC
    /// "background processing retries are idempotent").
    /// <para>
    /// Deliberately does NOT bump <see cref="Version"/>: firing is a system
    /// event, not a user edit, and must not invalidate the version an owner is
    /// holding in an open form. It also leaves <see cref="Status"/> and
    /// completion metadata untouched (BR "reminder delivery failure does not
    /// change task completion/state" — and neither does success).
    /// </para>
    /// </summary>
    public void TriggerReminder(DateTimeOffset triggeredAtUtc)
    {
        if (ReminderStatus != AgentTaskReminderStatus.Scheduled || ReminderEventId is not { } reminderEventId)
        {
            throw new InvalidOperationException(
                $"Task '{Id}' has no scheduled reminder to trigger (status '{ReminderStatus}').");
        }

        ReminderStatus = AgentTaskReminderStatus.Triggered;
        ReminderTriggeredAtUtc = triggeredAtUtc;

        AddDomainEvent(new AgentTaskReminderDueDomainEvent(
            reminderEventId, Id, Title, OwnerUserId, ReminderAtUtc!.Value, triggeredAtUtc));
    }

    private AgentTask()
    {
    }
}
