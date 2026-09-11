using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a ticket escalation
/// (CRM-138, ADR-005). Producer side only: the
/// <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the ticket update and its escalation-history row. The
/// notification, automation and reporting consumers are CRM-156/153/187
/// (YAGNI here).
/// <para>
/// The outbox row id is the consumer's idempotency key: one escalation produces
/// exactly one row, so a retrying consumer can deduplicate (BR "automatic
/// retries/consumers must be idempotent").
/// </para>
/// <para>
/// Target type and source travel as NAMES, not as enums: no string-enum
/// serializer is configured for the outbox payload, and a durable contract must
/// not encode them as ordinals that reordering an enum would silently change.
/// </para>
/// </summary>
internal sealed record TicketEscalatedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    string TicketNumber,
    int PreviousLevel,
    int NewLevel,
    string TargetType,
    Guid TargetId,
    string Reason,
    string Source,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-escalated.v1";

    public string Type => ContractName;
}
