using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a ticket ownership
/// change (CRM-136, ADR-005). Producer side only: the
/// <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the ticket update and its assignment-history row. The
/// notification and reporting consumers are CRM-156/CRM-188 (YAGNI here).
/// <para>
/// The outbox row id is the consumer's idempotency key: one ownership change
/// produces exactly one row, so a retrying consumer can deduplicate instead of
/// raising duplicate notifications (BR).
/// </para>
/// </summary>
internal sealed record TicketAssignedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    string TicketNumber,
    Guid? PreviousAgentId,
    Guid NewAgentId,
    string? Reason,
    TicketAssignmentSource Source,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-assigned.v1";

    public string Type => ContractName;
}
