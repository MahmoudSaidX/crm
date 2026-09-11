namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// What an escalation was routed to (CRM-138 Fields Dictionary). Only the two
/// target kinds this repo can actually validate are supported: an agent
/// (<c>IStaffSubjectReferenceReader</c>) and a department
/// (<c>IDepartmentActiveLookup</c>). The Fields Dictionary also mentions
/// "Role/Queue or supported target" — no queue model exists here, so adding one
/// would be invented business logic rather than a supported target.
/// </summary>
public enum TicketEscalationTargetType
{
    Agent,
    Department,
}

/// <summary>
/// How an escalation was applied (CRM-138 Fields Dictionary). Only
/// <see cref="Manual"/> is written today; the automatic-escalation stories
/// (CRM-153/154) write <see cref="Automation"/> through the same
/// <c>TicketService.EscalateAsync</c> capability rather than a parallel path
/// (BR "escalation uses the canonical escalation capability shared by manual
/// and automatic escalation").
/// </summary>
public enum TicketEscalationSource
{
    Manual,
    Automation,
}

/// <summary>
/// Append-only record of one escalation on a ticket (CRM-138). Written in the
/// SAME transaction/<c>SaveChanges</c> as the ticket update and its outbox row,
/// so a failed or retried save can never leave a history row without the
/// matching escalation, nor duplicate one.
/// <para>
/// A ticket therefore carries many historical escalation rows while the ticket
/// itself exposes exactly one current escalation state/level (BR). Escalation
/// -specific rather than a general ticket-history table, for the same reason
/// <see cref="TicketStatusHistory"/> is status-specific: the canonical ticket
/// history timeline is CRM-139 and owns that shape.
/// </para>
/// </summary>
public sealed class TicketEscalationHistory
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }

    /// <summary>Level before this escalation; 0 means the ticket was not escalated.</summary>
    public int PreviousLevel { get; init; }

    public int NewLevel { get; init; }

    public TicketEscalationTargetType TargetType { get; init; }

    public Guid TargetId { get; init; }

    /// <summary>Always required (Fields Dictionary): an escalation must be explained.</summary>
    public required string Reason { get; init; }

    public TicketEscalationSource Source { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string EscalatedBy { get; init; }

    public DateTimeOffset EscalatedAtUtc { get; init; }
}
