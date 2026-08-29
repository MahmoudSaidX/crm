using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

/// <summary>Thin Hangfire entry point; all durable state remains module-owned.</summary>
public sealed class ArchitectureFixtureOutboxJob(
    ArchitectureFixtureOutboxStore store,
    ArchitectureFixtureIntegrationEventDispatcher dispatcher,
    IOptions<OutboxProcessingOptions> options,
    ILogger<ArchitectureFixtureOutboxJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        OutboxProcessingOptions settings = options.Value;
        Guid leaseId = Guid.NewGuid();
        IReadOnlyList<ClaimedOutboxMessage> messages =
            await store.ClaimAsync(leaseId, DateTimeOffset.UtcNow, settings, cancellationToken);

        foreach (ClaimedOutboxMessage message in messages)
        {
            try
            {
                await dispatcher.DispatchAsync(message, cancellationToken);
                await store.MarkProcessedAsync(message.Id, leaseId, DateTimeOffset.UtcNow, cancellationToken);
                logger.LogInformation("Processed outbox message {OutboxMessageId} of type {IntegrationEventType}.", message.Id, message.Type);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Do not pass the exception object to logging: deserialization
                // errors can quote payload content. Persist/log only its type.
                logger.LogWarning(
                    "Outbox message {OutboxMessageId} processing failed with {ExceptionType}.",
                    message.Id,
                    exception.GetType().Name);
                await store.MarkFailedAsync(
                    message.Id,
                    leaseId,
                    DateTimeOffset.UtcNow,
                    settings,
                    exception.GetType().Name,
                    cancellationToken);
            }
        }
    }
}
