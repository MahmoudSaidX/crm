using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Raised inside this module when a ticket's owner changes (CRM-136).
/// Translated into <see cref="TicketAssignedIntegrationEvent"/> by
/// <see cref="Persistence.TicketManagementOutboxInterceptor"/> before it leaves
/// the module (ADR-005).
/// </summary>
public sealed record TicketAssignedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    Guid? PreviousAgentId,
    Guid NewAgentId,
    string? Reason,
    TicketAssignmentSource Source,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
