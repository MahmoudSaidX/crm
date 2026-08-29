using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class OutboxProcessingTests
{
    private static readonly OutboxProcessingOptions OptionsValue = new()
    {
        BatchSize = 20,
        RetryCeiling = 3,
        LeaseSeconds = 30,
        BackoffSeconds = 1,
    };

    [Fact]
    public async Task Job_DispatchesOnce_AndMarksProcessedAfterDurableReceipt()
    {
        Guid probeId = Guid.NewGuid();
        Guid messageId = await CreatePendingProbe(probeId, nameof(Job_DispatchesOnce_AndMarksProcessedAfterDurableReceipt));

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        ArchitectureFixtureOutboxJob job = CreateJob(context);

        await job.RunAsync(CancellationToken.None);
        await job.RunAsync(CancellationToken.None);

        OutboxMessage message = await context.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == messageId);
        IntegrationEventReceipt receipt = await context.IntegrationEventReceipts
            .AsNoTracking()
            .SingleAsync(row => row.EventId == messageId);

        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.LeaseId);
        Assert.Equal(probeId, receipt.ProbeId);
        Assert.Equal(1, await context.IntegrationEventReceipts.CountAsync(row => row.EventId == messageId));

        await Cleanup(probeId, messageId);
    }

    [Fact]
    public async Task Job_Failure_ReleasesLeaseAndAppliesBoundedBackoff()
    {
        Guid messageId = Guid.NewGuid();
        await using (ArchitectureFixtureDbContext setup = PostgresTestDatabase.CreateContext())
        {
            setup.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                Type = "unsupported.v1",
                Payload = "{}",
                OccurredAtUtc = DateTimeOffset.UtcNow,
                CorrelationId = "test",
            });
            await setup.SaveChangesAsync();
        }

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await CreateJob(context).RunAsync(CancellationToken.None);

        OutboxMessage failed = await context.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == messageId);
        Assert.Null(failed.ProcessedAtUtc);
        Assert.Null(failed.LeaseId);
        Assert.Equal(1, failed.RetryCount);
        Assert.Equal(nameof(InvalidOperationException), failed.Error);
        Assert.NotNull(failed.NextAttemptAtUtc);

        for (int attempt = 1; attempt < OptionsValue.RetryCeiling; attempt++)
        {
            await context.OutboxMessages
                .Where(row => row.Id == messageId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    row => row.NextAttemptAtUtc,
                    DateTimeOffset.UtcNow.AddSeconds(-1)));
            context.ChangeTracker.Clear();
            await CreateJob(context).RunAsync(CancellationToken.None);
        }

        context.ChangeTracker.Clear();
        OutboxMessage exhausted = await context.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == messageId);
        Assert.Equal(OptionsValue.RetryCeiling, exhausted.RetryCount);
        Assert.Null(exhausted.NextAttemptAtUtc);

        await CreateJob(context).RunAsync(CancellationToken.None);
        context.ChangeTracker.Clear();
        Assert.Equal(
            OptionsValue.RetryCeiling,
            (await context.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == messageId)).RetryCount);

        await context.OutboxMessages.Where(row => row.Id == messageId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ExistingReceipt_MakesAtLeastOnceRedeliveryIdempotent()
    {
        Guid probeId = Guid.NewGuid();
        Guid messageId = await CreatePendingProbe(probeId, nameof(ExistingReceipt_MakesAtLeastOnceRedeliveryIdempotent));

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        context.IntegrationEventReceipts.Add(new IntegrationEventReceipt
        {
            EventId = messageId,
            EventType = "architecture-fixture.probe-recorded.v1",
            ProbeId = probeId,
            ProbeLabel = nameof(ExistingReceipt_MakesAtLeastOnceRedeliveryIdempotent),
            ConsumedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        await CreateJob(context).RunAsync(CancellationToken.None);

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.IntegrationEventReceipts.CountAsync(row => row.EventId == messageId));
        Assert.NotNull(
            (await context.OutboxMessages.AsNoTracking().SingleAsync(row => row.Id == messageId)).ProcessedAtUtc);

        await Cleanup(probeId, messageId);
    }

    [Fact]
    public async Task ConcurrentJobs_ConsumeEachEventExactlyOnce()
    {
        Guid[] probeIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        Guid[] messageIds = new Guid[probeIds.Length];
        for (int index = 0; index < probeIds.Length; index++)
        {
            messageIds[index] = await CreatePendingProbe(probeIds[index], $"concurrent-{index}");
        }

        await using ArchitectureFixtureDbContext firstContext = PostgresTestDatabase.CreateContext();
        await using ArchitectureFixtureDbContext secondContext = PostgresTestDatabase.CreateContext();

        await Task.WhenAll(
            CreateJob(firstContext).RunAsync(CancellationToken.None),
            CreateJob(secondContext).RunAsync(CancellationToken.None));

        await using ArchitectureFixtureDbContext assertionContext = PostgresTestDatabase.CreateContext();
        Assert.Equal(
            messageIds.Length,
            await assertionContext.IntegrationEventReceipts.CountAsync(row => messageIds.Contains(row.EventId)));
        Assert.Equal(
            messageIds.Length,
            await assertionContext.OutboxMessages.CountAsync(row => messageIds.Contains(row.Id) && row.ProcessedAtUtc != null));

        for (int index = 0; index < probeIds.Length; index++)
        {
            await Cleanup(probeIds[index], messageIds[index]);
        }
    }

    private static ArchitectureFixtureOutboxJob CreateJob(ArchitectureFixtureDbContext context) =>
        new(
            new ArchitectureFixtureOutboxStore(context),
            new ArchitectureFixtureIntegrationEventDispatcher(context),
            Options.Create(OptionsValue),
            NullLogger<ArchitectureFixtureOutboxJob>.Instance);

    private static async Task<Guid> CreatePendingProbe(Guid probeId, string label)
    {
        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        context.PersistenceProbes.Add(PersistenceProbe.Record(probeId, label, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
        return await context.OutboxMessages
            .Where(message => message.Payload.Contains(probeId.ToString()))
            .Select(message => message.Id)
            .SingleAsync();
    }

    private static async Task Cleanup(Guid probeId, Guid messageId)
    {
        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await context.IntegrationEventReceipts.Where(row => row.EventId == messageId).ExecuteDeleteAsync();
        await context.OutboxMessages.Where(row => row.Id == messageId).ExecuteDeleteAsync();
        await context.PersistenceProbes.Where(row => row.Id == probeId).ExecuteDeleteAsync();
    }
}
