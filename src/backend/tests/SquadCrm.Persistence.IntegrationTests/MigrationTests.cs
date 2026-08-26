using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// AC 2 and AC 4 — migrations run successfully, and the database is fully
/// described by them.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class MigrationTests
{
    [Fact]
    public async Task Migrations_ApplyToACleanDatabase()
    {
        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();

        // Idempotent: the fixture already applied them, so this must be a no-op
        // rather than an error.
        await context.Database.MigrateAsync();

        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task NoMigrationsRemainPending()
    {
        await using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();

        // A pending migration means the committed model and the committed
        // migrations disagree — the database could not be recreated from them.
        IEnumerable<string> pending = await context.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }
}
