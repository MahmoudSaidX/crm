using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a ticket watcher
/// membership change (CRM-147, ADR-005). Producer side only: the
/// <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the watcher row and its history row. The consumer that
/// actually delivers "configured ticket updates" to a watcher is CRM-155/156
/// (YAGNI here).
/// <para>
/// <see cref="Action"/> travels as a NAME, not as an enum: no string-enum
/// serializer is configured for the outbox payload, and a durable contract
/// must not encode it as an ordinal that reordering an enum would silently
/// change.
/// </para>
/// </summary>
internal sealed record TicketWatcherChangedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    Guid UserId,
    string Action,
    string ChangedBy,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-watcher-changed.v1";

    public string Type => ContractName;
}
