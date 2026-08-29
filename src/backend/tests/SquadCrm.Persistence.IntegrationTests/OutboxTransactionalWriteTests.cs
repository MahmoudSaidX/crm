using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SquadCrm.Modules.ArchitectureFixture.Contracts;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Proves CRM-198's core guarantee: a domain event raised on a tracked entity
/// is translated into an <c>OutboxMessage</c> row that commits in the SAME
/// database transaction as the business row, for every
/// <c>SaveChanges</c>/<c>SaveChangesAsync</c> overload — not just the async
/// one a naive <c>SaveChangesAsync(CancellationToken)</c> override would have
/// covered.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class OutboxTransactionalWriteTests
{
    [Fact]
    public async Task Probe_And_OutboxMessage_CommitTogether()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset recordedAt = new(2026, 8, 26, 9, 34, 56, TimeSpan.Zero);

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();

        // Proves the interceptor works even when the caller has already
        // opened an explicit transaction (B1) — exactly the shape
        // PersistenceRoundTripTests uses.
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        context.PersistenceProbes.Add(
            PersistenceProbe.Record(id, nameof(Probe_And_OutboxMessage_CommitTogether), recordedAt));

        await context.SaveChangesAsync();

        PersistenceProbe reloadedProbe = await context.PersistenceProbes
            .AsNoTracking()
            .SingleAsync(probe => probe.Id == id);
        Assert.Equal(nameof(Probe_And_OutboxMessage_CommitTogether), reloadedProbe.Label);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.Type == ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName
                && message.Payload.Contains(id.ToString()));

        Assert.Equal(ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName, outboxMessage.Type);
        Assert.Null(outboxMessage.ProcessedAtUtc);
        Assert.Equal(0, outboxMessage.RetryCount);
        Assert.Null(outboxMessage.Error);
        Assert.False(string.IsNullOrWhiteSpace(outboxMessage.CorrelationId));
        Assert.Contains(id.ToString(), outboxMessage.Payload);

        // Scaffolding must not accumulate rows in a developer's database.
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task SaveChanges_NonAsyncOverload_AlsoWritesOutboxRow()
    {
        // The exact defect the review turned on (B1): a
        // SaveChangesAsync(CancellationToken) override would miss this
        // synchronous overload entirely, silently losing the outbox row.
        Guid id = Guid.NewGuid();
        DateTimeOffset recordedAt = new(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        context.PersistenceProbes.Add(
            PersistenceProbe.Record(id, nameof(SaveChanges_NonAsyncOverload_AlsoWritesOutboxRow), recordedAt));

        // Synchronous overload — proves the interceptor's `SavingChanges`
        // hook (not only `SavingChangesAsync`) fires.
        context.SaveChanges();

        PersistenceProbe reloadedProbe = await context.PersistenceProbes
            .AsNoTracking()
            .SingleAsync(probe => probe.Id == id);
        Assert.Equal(nameof(SaveChanges_NonAsyncOverload_AlsoWritesOutboxRow), reloadedProbe.Label);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Payload.Contains(id.ToString()));

        Assert.Equal(ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName, outboxMessage.Type);
        Assert.Null(outboxMessage.ProcessedAtUtc);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task SaveChanges_WithBoolOverload_AlsoWritesOutboxRow()
    {
        // The other overload a SaveChangesAsync(CancellationToken) override
        // would miss: SaveChanges(bool acceptAllChangesOnSuccess).
        Guid id = Guid.NewGuid();
        DateTimeOffset recordedAt = new(2026, 8, 26, 11, 0, 0, TimeSpan.Zero);

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        context.PersistenceProbes.Add(
            PersistenceProbe.Record(id, nameof(SaveChanges_WithBoolOverload_AlsoWritesOutboxRow), recordedAt));

        context.SaveChanges(acceptAllChangesOnSuccess: true);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Payload.Contains(id.ToString()));

        Assert.Equal(ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName, outboxMessage.Type);

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task SaveChanges_WithNoPendingDomainEvents_WritesNoOutboxRow()
    {
        // A save with no HasDomainEvents-raising change is a no-op for the
        // interceptor: it must not add spurious rows.
        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        int outboxCountBefore = await context.OutboxMessages.CountAsync();

        await context.SaveChangesAsync();

        int outboxCountAfter = await context.OutboxMessages.CountAsync();
        Assert.Equal(outboxCountBefore, outboxCountAfter);

        await transaction.RollbackAsync();
    }
}
