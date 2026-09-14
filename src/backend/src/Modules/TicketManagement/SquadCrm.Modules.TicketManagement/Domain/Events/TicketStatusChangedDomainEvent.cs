using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Domain.Entities;
using SquadCrm.Modules.TicketManagement.Infrastructure.Outbox;

namespace SquadCrm.Modules.TicketManagement.Domain.Events;

/// <summary>
/// Raised inside this module when a ticket's lifecycle status changes
/// (CRM-137). Translated into <see cref="TicketStatusChangedIntegrationEvent"/>
/// by <see cref="TicketManagementOutboxInterceptor"/> before it
/// leaves the module (ADR-005).
/// </summary>
public sealed record TicketStatusChangedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    TicketStatus PreviousStatus,
    TicketStatus NewStatus,
    string? Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
