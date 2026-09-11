using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a ticket lifecycle
/// status change (CRM-137, ADR-005). Producer side only: the
/// <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the ticket update and its status-history row. The SLA,
/// notification, reporting and automation consumers are
/// CRM-150/156/188/153 (YAGNI here).
/// <para>
/// The outbox row id is the consumer's idempotency key: one status change
/// produces exactly one row, so a retrying consumer can deduplicate.
/// </para>
/// <para>
/// The statuses travel as NAMES, not as the enum: no string-enum serializer is
/// configured for the outbox payload, and a durable contract must not encode a
/// status as an ordinal that reordering the enum would silently change.
/// </para>
/// </summary>
internal sealed record TicketStatusChangedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    string TicketNumber,
    string PreviousStatus,
    string NewStatus,
    string? Reason,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-status-changed.v1";

    public string Type => ContractName;
}
