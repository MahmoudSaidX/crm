using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// A module using ordinary EF transactions <b>within its own context</b>. No
/// cross-module transaction is attempted: that is not the integration mechanism
/// (contracts and events are), and CRM-198 owns the durable path.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class PersistenceRoundTripTests
{
    [Fact]
    public async Task Probe_CanBeWrittenAndReadBack()
    {
        Guid id = Guid.NewGuid();

        // PostgreSQL `timestamptz` stores an instant, so Npgsql accepts only a
        // UTC (offset-zero) DateTimeOffset and rejects a local-offset value
        // outright rather than guessing a time zone. Callers convert first.
        DateTimeOffset recordedAt = new(2026, 8, 26, 9, 34, 56, TimeSpan.Zero);

        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        context.PersistenceProbes.Add(new PersistenceProbe
        {
            Id = id,
            Label = nameof(Probe_CanBeWrittenAndReadBack),
            RecordedAtUtc = recordedAt,
        });

        await context.SaveChangesAsync();

        PersistenceProbe reloaded = await context.PersistenceProbes
            .AsNoTracking()
            .SingleAsync(probe => probe.Id == id);

        Assert.Equal(nameof(Probe_CanBeWrittenAndReadBack), reloaded.Label);

        Assert.Equal(recordedAt, reloaded.RecordedAtUtc.ToUniversalTime());

        // Scaffolding must not accumulate rows in a developer's database.
        await transaction.RollbackAsync();
    }
}
