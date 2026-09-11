using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.TicketManagement.Events;

namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// One internal collaboration note on a ticket (CRM-147). Append-only: there
/// is no edit or delete path, because internal collaboration is auditable
/// history (BR) and the ticket timeline projects these rows directly.
/// <para>
/// Internal-only <b>by construction</b>: unlike
/// <see cref="TicketStatusHistory"/>, this type carries no visibility column.
/// A note has no customer-visible representation at all, so no flag exists
/// that a bug could flip to expose one through the customer projection
/// (AC "customer-facing APIs never expose internal notes"). Customer-visible
/// messages are Conversation's data (CRM-164), a different capability
/// entirely (BR).
/// </para>
/// <para>
/// Raises <see cref="TicketNoteAddedDomainEvent"/> rather than mutating the
/// <see cref="Ticket"/> aggregate: a note is not a ticket state change, and
/// <see cref="TicketManagementOutboxInterceptor"/> drains events from any
/// tracked <see cref="HasDomainEvents"/> entity, so the outbox row still
/// commits in the same transaction as the note.
/// </para>
/// </summary>
public sealed class TicketInternalNote : HasDomainEvents
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }

    /// <summary>Trimmed, bounded, internal-only free text.</summary>
    public required string Body { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string CreatedBy { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public static TicketInternalNote Create(
        Guid id,
        Guid ticketId,
        string body,
        string createdBy,
        IReadOnlyList<Guid> mentionedUserIds,
        DateTimeOffset createdAtUtc)
    {
        TicketInternalNote note = new()
        {
            Id = id,
            TicketId = ticketId,
            Body = body,
            CreatedBy = createdBy,
            CreatedAtUtc = createdAtUtc,
        };

        // Raised from the factory so a note can never be persisted without its
        // event — the same reason Ticket.Create/Escalate raise from inside the
        // entity rather than from the service.
        note.AddDomainEvent(new TicketNoteAddedDomainEvent(
            ticketId, id, mentionedUserIds, createdBy, createdAtUtc));
        return note;
    }
}

/// <summary>
/// A validated mention of a staff user on a <see cref="TicketInternalNote"/>
/// (CRM-147). Stored as explicit ids rather than parsed out of the note body:
/// free-text parsing cannot enforce the Business Rule that a mentioned user
/// must have access to the ticket, whereas an id list validated against
/// StaffIdentity at write time can.
/// </summary>
public sealed class TicketNoteMention
{
    public Guid Id { get; init; }
    public Guid NoteId { get; init; }

    /// <summary>
    /// Denormalized from the note so the timeline and future notification
    /// consumers can filter mentions by ticket without joining.
    /// </summary>
    public Guid TicketId { get; init; }

    public Guid MentionedUserId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
