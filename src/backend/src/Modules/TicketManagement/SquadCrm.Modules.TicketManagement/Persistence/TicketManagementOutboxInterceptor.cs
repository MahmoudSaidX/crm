using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.TicketManagement.Events;

namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// Drains domain events raised on any tracked <see cref="HasDomainEvents"/>
/// entity, translates each into this module's <c>IIntegrationEvent</c>
/// contract(s), and adds the corresponding <see cref="OutboxMessage"/> row to
/// the SAME change tracker before <c>SaveChanges</c> commits — proving
/// atomicity via EF Core's single-transaction guarantee (CRM-198, B1). Mirrors
/// <c>ArchitectureFixtureOutboxInterceptor</c> exactly.
/// <para>
/// Module-internal and module-specific by design: the domain-event-to-
/// integration-event translation is this module's own knowledge. There is no
/// generic/shared cross-module event-translation framework (YAGNI).
/// </para>
/// </summary>
internal sealed class TicketManagementOutboxInterceptor(ICorrelationIdAccessor? correlationIdAccessor = null)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxMessagesForPendingDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddOutboxMessagesForPendingDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessagesForPendingDomainEvents(DbContext? context)
    {
        if (context is not TicketManagementDbContext ticketManagementContext)
        {
            return;
        }

        string correlationId = correlationIdAccessor?.Current ?? Guid.NewGuid().ToString("n");
        DateTimeOffset writtenAtUtc = DateTimeOffset.UtcNow;

        // Snapshot first: adding OutboxMessage rows to the change tracker
        // below (via ticketManagementContext.OutboxMessages.Add(...)) mutates
        // the same tracker we are enumerating, which throws
        // InvalidOperationException ("Collection was modified") if we
        // enumerate it lazily.
        List<EntityEntry<HasDomainEvents>> entries = ticketManagementContext.ChangeTracker.Entries<HasDomainEvents>().ToList();

        foreach (EntityEntry<HasDomainEvents> entry in entries)
        {
            HasDomainEvents entity = entry.Entity;

            if (entity.DomainEvents.Count == 0)
            {
                continue;
            }

            foreach (IDomainEvent domainEvent in entity.DomainEvents)
            {
                IIntegrationEvent integrationEvent = Translate(domainEvent);

                ticketManagementContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = integrationEvent.EventId,
                    Type = integrationEvent.Type,
                    Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
                    OccurredAtUtc = writtenAtUtc,
                    CorrelationId = correlationId,
                });
            }

            entity.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Explicit per-event-type translation. A new domain event type without a
    /// matching branch throws rather than being silently dropped (N8) — the
    /// next author adding a second event type must extend this switch.
    /// </summary>
    private static IIntegrationEvent Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        TicketCreatedDomainEvent created => new TicketCreatedIntegrationEvent(
            Guid.NewGuid(), created.TicketId, created.TicketNumber, created.CustomerId, created.OccurredAtUtc),
        _ => throw new InvalidOperationException(
            $"No integration-event translation registered for domain event type '{domainEvent.GetType()}'."),
    };
}
