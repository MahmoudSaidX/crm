using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.Modules.TicketManagement.Domain.Entities;
using SquadCrm.Modules.TicketManagement.Infrastructure.Outbox;

namespace SquadCrm.Modules.TicketManagement.Domain.Events;

/// <summary>
/// Raised inside this module when a ticket is escalated (CRM-138). Translated
/// into <see cref="TicketEscalatedIntegrationEvent"/> by
/// <see cref="TicketManagementOutboxInterceptor"/> before it leaves
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
