using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.TicketManagement.Domain.Events;

/// <summary>
/// Raised inside this module when a <see cref="Ticket"/> is created
/// (CRM-133). Never crosses the module boundary directly — this module's own
/// <see cref="TicketManagementOutboxInterceptor"/> translates it
/// into the explicit <see cref="TicketCreatedIntegrationEvent"/> contract
/// before it leaves the module (ADR-005).
/// </summary>
public sealed record TicketCreatedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    Guid CustomerId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
