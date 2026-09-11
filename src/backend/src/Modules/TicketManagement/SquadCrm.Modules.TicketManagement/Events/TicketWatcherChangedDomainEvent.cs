using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Raised inside this module when a user starts or stops watching a ticket
/// (CRM-147). Translated into
/// <see cref="TicketWatcherChangedIntegrationEvent"/> by
/// <see cref="Persistence.TicketManagementOutboxInterceptor"/> before it leaves
/// the module (ADR-005).
/// </summary>
public sealed record TicketWatcherChangedDomainEvent(
    Guid TicketId,
    Guid UserId,
    TicketWatcherAction Action,
    string ChangedBy,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
