using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.ArchitectureFixture.Contracts;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

public sealed class ArchitectureFixtureIntegrationEventDispatcher(ArchitectureFixtureDbContext dbContext)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal async Task DispatchAsync(ClaimedOutboxMessage message, CancellationToken cancellationToken)
    {
        if (message.Type != ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName)
        {
            throw new InvalidOperationException($"Unsupported integration event type '{message.Type}'.");
        }

        ArchitectureFixtureProbeRecordedIntegrationEvent integrationEvent =
            JsonSerializer.Deserialize<ArchitectureFixtureProbeRecordedIntegrationEvent>(message.Payload, SerializerOptions)
            ?? throw new InvalidOperationException($"Integration event '{message.Id}' has an empty payload.");

        if (integrationEvent.EventId != message.Id)
        {
            throw new InvalidOperationException($"Integration event payload identity does not match outbox message '{message.Id}'.");
        }

        bool alreadyConsumed = await dbContext.IntegrationEventReceipts
            .AnyAsync(receipt => receipt.EventId == integrationEvent.EventId, cancellationToken);
        if (alreadyConsumed)
        {
            return;
        }

        dbContext.IntegrationEventReceipts.Add(new IntegrationEventReceipt
        {
            EventId = integrationEvent.EventId,
            EventType = integrationEvent.Type,
            ProbeId = integrationEvent.ProbeId,
            ProbeLabel = integrationEvent.Label,
            ConsumedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            bool wonByConcurrentConsumer = await dbContext.IntegrationEventReceipts
                .AsNoTracking()
                .AnyAsync(receipt => receipt.EventId == integrationEvent.EventId, cancellationToken);
            if (!wonByConcurrentConsumer)
            {
                throw;
            }
        }
    }
}
