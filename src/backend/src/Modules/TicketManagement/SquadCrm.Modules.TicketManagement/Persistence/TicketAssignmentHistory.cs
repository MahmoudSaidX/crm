namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// How an assignment was applied (CRM-136 Fields Dictionary). Only
/// <see cref="Manual"/> is written today; the automatic-assignment stories
/// (CRM-151/152) write <see cref="Automation"/> through the same
/// <c>TicketService.AssignAsync</c> capability rather than a parallel path.
/// </summary>
public enum TicketAssignmentSource
{
    Manual,
    Automation,
}

/// <summary>
/// Append-only record of one ownership change on a ticket (CRM-136). Written
/// in the SAME transaction/<c>SaveChanges</c> as the ticket update, so a failed
/// or retried save can never leave a history row without the matching owner
/// change, nor duplicate one (BR "event consumers and retries must not create
/// duplicate assignment history").
/// <para>
/// Assignment-specific rather than a general ticket-history table: the
/// canonical ticket history timeline is CRM-139, which owns that shape. This
/// story records the assignment facts its own AC requires without inventing
/// the timeline model ahead of that story.
/// </para>
/// </summary>
public sealed class TicketAssignmentHistory
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }

    /// <summary>Owner before the change; null when the ticket was unassigned.</summary>
    public Guid? PreviousAgentId { get; init; }

    public Guid NewAgentId { get; init; }

    /// <summary>Required when replacing an existing owner; optional otherwise.</summary>
    public string? Reason { get; init; }

    public TicketAssignmentSource Source { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string ChangedBy { get; init; }

    public DateTimeOffset ChangedAtUtc { get; init; }
}
