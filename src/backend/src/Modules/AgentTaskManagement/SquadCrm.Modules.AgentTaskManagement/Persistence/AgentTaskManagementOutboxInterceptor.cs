using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.AgentTaskManagement.Events;

namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

/// <summary>
/// Drains domain events raised on any tracked <see cref="HasDomainEvents"/>
/// entity, translates each into this module's <c>IIntegrationEvent</c>
/// contract(s), and adds the corresponding <see cref="OutboxMessage"/> row to
/// the SAME change tracker before <c>SaveChanges</c> commits — proving
/// atomicity via EF Core's single-transaction guarantee (CRM-198, B1). Mirrors
/// <c>TicketManagementOutboxInterceptor</c> exactly.
/// </summary>
internal sealed class AgentTaskManagementOutboxInterceptor(ICorrelationIdAccessor? correlationIdAccessor = null)
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
        if (context is not AgentTaskManagementDbContext agentTaskManagementContext)
        {
            return;
        }

        string correlationId = correlationIdAccessor?.Current ?? Guid.NewGuid().ToString("n");
        DateTimeOffset writtenAtUtc = DateTimeOffset.UtcNow;

        // Snapshot first: adding OutboxMessage rows to the change tracker
        // below mutates the same tracker we are enumerating.
        List<EntityEntry<HasDomainEvents>> entries =
            agentTaskManagementContext.ChangeTracker.Entries<HasDomainEvents>().ToList();

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

                agentTaskManagementContext.OutboxMessages.Add(new OutboxMessage
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
    /// matching branch throws rather than being silently dropped (N8).
    /// </summary>
    private static IIntegrationEvent Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        AgentTaskCreatedDomainEvent created => new AgentTaskCreatedIntegrationEvent(
            Guid.NewGuid(), created.TaskId, created.Title, created.OwnerUserId, created.DueAtUtc, created.OccurredAtUtc),
        AgentTaskCompletedDomainEvent completed => new AgentTaskCompletedIntegrationEvent(
            Guid.NewGuid(), completed.TaskId, completed.Title, completed.OwnerUserId, completed.CompletedAtUtc, completed.OccurredAtUtc),
        // The reminder occurrence id is passed through, NOT regenerated: it
        // becomes the outbox row's primary key below, which is what makes a
        // retried reminder sweep idempotent (CRM-144 BR).
        AgentTaskReminderDueDomainEvent reminderDue => new AgentTaskReminderDueIntegrationEvent(
            reminderDue.ReminderEventId, reminderDue.TaskId, reminderDue.Title, reminderDue.OwnerUserId, reminderDue.ReminderAtUtc, reminderDue.OccurredAtUtc),
        AgentTaskReopenedDomainEvent reopened => new AgentTaskReopenedIntegrationEvent(
            Guid.NewGuid(), reopened.TaskId, reopened.Title, reopened.OwnerUserId, reopened.OccurredAtUtc),
        _ => throw new InvalidOperationException(
            $"No integration-event translation registered for domain event type '{domainEvent.GetType()}'."),
    };
}
