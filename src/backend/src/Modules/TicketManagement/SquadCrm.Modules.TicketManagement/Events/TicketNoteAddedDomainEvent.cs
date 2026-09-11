using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Raised inside this module when an internal collaboration note is added to a
/// ticket (CRM-147). Translated into
/// <see cref="TicketNoteAddedIntegrationEvent"/> by
/// <see cref="Persistence.TicketManagementOutboxInterceptor"/> before it leaves
/// the module (ADR-005).
/// <para>
/// Carries the mentioned user ids but <b>not the note body</b> — see the
/// integration event's remarks.
/// </para>
/// </summary>
public sealed record TicketNoteAddedDomainEvent(
    Guid TicketId,
    Guid NoteId,
    IReadOnlyList<Guid> MentionedUserIds,
    string CreatedBy,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
