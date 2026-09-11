namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// Append-only record of one lifecycle status change on a ticket (CRM-137).
/// Written in the SAME transaction/<c>SaveChanges</c> as the ticket update and
/// its outbox row, so a failed or retried save can never leave a history row
/// without the matching status change, nor duplicate one.
/// <para>
/// Status-specific rather than a general ticket-history table, for the same
/// reason <see cref="TicketAssignmentHistory"/> is assignment-specific: the
/// canonical ticket history timeline is CRM-139 and owns that shape. Reopening
/// appends a row; it never rewrites or deletes the earlier resolution/closure
/// rows (BR).
/// </para>
/// </summary>
public sealed class TicketStatusHistory
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public TicketStatus PreviousStatus { get; init; }
    public TicketStatus NewStatus { get; init; }

    /// <summary>Required for close/reopen transitions; optional otherwise.</summary>
    public string? Reason { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string ChangedBy { get; init; }

    public DateTimeOffset ChangedAtUtc { get; init; }
}
