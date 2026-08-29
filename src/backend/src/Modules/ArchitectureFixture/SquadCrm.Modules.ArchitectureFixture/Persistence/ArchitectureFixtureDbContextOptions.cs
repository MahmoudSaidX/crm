using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Correlation;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// The <b>single</b> place this module's provider options are applied. The
/// runtime registration in <c>ArchitectureFixtureModule.RegisterServices</c> and
/// the design-time <see cref="ArchitectureFixtureDbContextFactory"/> both call
/// it, so the provider configuration, the migrations-history placement and the
/// outbox interceptor wiring (CRM-198, B1) can never diverge between
/// <c>dotnet ef</c>/tests and the running application.
/// </summary>
internal static class ArchitectureFixtureDbContextOptions
{
    /// <summary>
    /// Applies Npgsql with this module's own migration-history table, inside
    /// this module's own schema, and registers
    /// <see cref="ArchitectureFixtureOutboxInterceptor"/> so every
    /// <c>SaveChanges</c>/<c>SaveChangesAsync</c> overload writes any pending
    /// outbox rows in the same transaction as the business change.
    /// <paramref name="correlationIdAccessor"/> is <c>null</c> outside an HTTP
    /// request (design time, tests) — the interceptor falls back to a freshly
    /// generated id per save in that case.
    /// </summary>
    public static DbContextOptionsBuilder Apply(
        DbContextOptionsBuilder options,
        string connectionString,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    ArchitectureFixtureSchema.MigrationsHistoryTable,
                    ArchitectureFixtureSchema.Name))
            .AddInterceptors(new ArchitectureFixtureOutboxInterceptor(correlationIdAccessor));
    }
}
