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

        AddDomainEvent(new AgentTaskCompletedDomainEvent(Id, Title, OwnerUserId, completedAtUtc, completedAtUtc));
    }

    /// <summary>
    /// Applies the explicitly supported reopen workflow (BR: completed tasks
    /// are immutable in completion metadata except through this transition).
    /// Eligibility (completed, owner, version) is enforced by
    /// <c>AgentTaskService</c> before this is called.
    /// </summary>
    public void Reopen(DateTimeOffset reopenedAtUtc)
    {
        Status = AgentTaskStatus.Open;
        CompletedAtUtc = null;
        UpdatedAtUtc = reopenedAtUtc;
        Version++;

        AddDomainEvent(new AgentTaskReopenedDomainEvent(Id, Title, OwnerUserId, reopenedAtUtc));
    }

    private AgentTask()
    {
    }
}
