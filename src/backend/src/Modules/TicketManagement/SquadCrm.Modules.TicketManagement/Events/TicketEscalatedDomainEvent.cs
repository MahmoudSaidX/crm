using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.Events;

/// <summary>
/// Raised inside this module when a ticket is escalated (CRM-138). Translated
/// into <see cref="TicketEscalatedIntegrationEvent"/> by
/// <see cref="Persistence.TicketManagementOutboxInterceptor"/> before it leaves
/// the module (ADR-005).
/// </summary>
public sealed record TicketEscalatedDomainEvent(
    Guid TicketId,
    string TicketNumber,
    int PreviousLevel,
    int NewLevel,
    TicketEscalationTargetType TargetType,
    Guid TargetId,
    string Reason,
    TicketEscalationSource Source,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
