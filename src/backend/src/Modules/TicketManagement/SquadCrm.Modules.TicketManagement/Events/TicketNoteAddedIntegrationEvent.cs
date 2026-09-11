using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for an internal ticket
/// note and the mentions it carries (CRM-147, ADR-005). Producer side only:
/// the <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the note and its mention rows. The notification consumer is
/// CRM-155/156 (YAGNI here); this event is what makes a mention notification
/// "durable" as the AC requires — the mention survives a crashed or retried
/// consumer because it is committed with the note itself.
/// <para>
/// <b>The note body is deliberately absent.</b> The outbox is a
/// cross-module/external boundary, and internal discussion must not leak
/// through an event payload any more than through an API response (AC
/// "customer-facing APIs/portal/conversations never expose internal notes").
/// A consumer that legitimately needs the text reads it back through this
/// module's authorized endpoint, where the caller's permission is checked;
/// <see cref="NoteId"/> is what makes that possible.
/// </para>
/// <para>
/// The outbox row id is the consumer's idempotency key: one note produces
/// exactly one row, so a retrying consumer can deduplicate and will not
/// re-notify a mentioned user.
/// </para>
/// </summary>
internal sealed record TicketNoteAddedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    Guid NoteId,
    IReadOnlyList<Guid> MentionedUserIds,
    string CreatedBy,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-note-added.v1";

    public string Type => ContractName;
}
