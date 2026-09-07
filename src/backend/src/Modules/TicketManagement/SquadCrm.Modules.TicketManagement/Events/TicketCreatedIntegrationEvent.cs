using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Explicit, versionable cross-module/external contract for a created ticket
/// (CRM-133, ADR-005). Only the producer side is built by this story — the
/// <see cref="Persistence.OutboxMessage"/> row is written in the same
/// transaction as the <see cref="Persistence.Ticket"/> insert; no
/// dispatcher/consumer reads it yet (YAGNI, no downstream SLA/routing/
/// reporting consumer exists).
/// </summary>
internal sealed record TicketCreatedIntegrationEvent(
    Guid EventId,
    Guid TicketId,
    string TicketNumber,
    Guid CustomerId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "ticket-management.ticket-created.v1";

    public string Type => ContractName;
}
