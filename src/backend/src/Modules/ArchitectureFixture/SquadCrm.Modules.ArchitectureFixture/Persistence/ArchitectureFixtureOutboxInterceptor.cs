using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Drains domain events raised on any tracked <see cref="HasDomainEvents"/>
/// entity, translates each into this module's <c>IIntegrationEvent</c>
/// contract(s), and adds the corresponding <see cref="OutboxMessage"/> row to
/// the SAME change tracker before <c>SaveChanges</c> commits — proving
/// atomicity via EF Core's single-transaction guarantee (CRM-198, B1).
/// <para>
/// Fires for every <c>SaveChanges</c>/<c>SaveChangesAsync</c> overload (unlike
/// a <c>SaveChangesAsync(CancellationToken)</c> override, which misses
/// <c>SaveChanges()</c>, <c>SaveChanges(bool)</c> and
/// <c>SaveChangesAsync(bool, CancellationToken)</c>) and behaves identically
/// whether or not the caller already opened an explicit
/// <c>BeginTransactionAsync</c> — EF Core enlists the interceptor's added rows
/// in whichever transaction is already open.
/// </para>
/// <para>
/// Module-internal and module-specific by design: the domain-event-to-
/// integration-event translation is this module's own knowledge. There is no
/// generic/shared cross-module event-translation framework (YAGNI) — a second
/// module defines its own interceptor the same way.
/// </para>
/// </summary>
internal sealed class ArchitectureFixtureOutboxInterceptor(ICorrelationIdAccessor? correlationIdAccessor = null)
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
        if (context is not ArchitectureFixtureDbContext fixtureContext)
        {
            return;
        }

        string correlationId = correlationIdAccessor?.Current ?? Guid.NewGuid().ToString("n");
        DateTimeOffset writtenAtUtc = DateTimeOffset.UtcNow;

        // Snapshot first: adding OutboxMessage rows to the change tracker
        // below (via fixtureContext.OutboxMessages.Add(...)) mutates the
        // same tracker we are enumerating, which throws
        // InvalidOperationException ("Collection was modified") if we
        // enumerate it lazily.
        List<EntityEntry<HasDomainEvents>> entries = fixtureContext.ChangeTracker.Entries<HasDomainEvents>().ToList();

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

                fixtureContext.OutboxMessages.Add(new OutboxMessage
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
    /// next module author adding a second event type must extend this switch.
    /// </summary>
    private static IIntegrationEvent Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        PersistenceProbeRecordedDomainEvent recorded => new ArchitectureFixtureProbeRecordedIntegrationEvent(
            Guid.NewGuid(), recorded.ProbeId, recorded.Label, recorded.OccurredAtUtc),
        _ => throw new InvalidOperationException(
            $"No integration-event translation registered for domain event type '{domainEvent.GetType()}'."),
    };
}
