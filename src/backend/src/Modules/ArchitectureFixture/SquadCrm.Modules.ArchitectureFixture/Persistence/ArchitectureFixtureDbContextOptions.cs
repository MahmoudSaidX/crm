using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// The <b>single</b> place this module's provider options are applied. The
/// runtime registration in <c>ArchitectureFixtureModule.RegisterServices</c> and
/// the design-time <see cref="ArchitectureFixtureDbContextFactory"/> both call
/// it, so the provider configuration and the migrations-history placement can
/// never diverge between <c>dotnet ef</c> and the running application.
/// </summary>
internal static class ArchitectureFixtureDbContextOptions
{
    /// <summary>
    /// Applies Npgsql with this module's own migration-history table, inside
    /// this module's own schema.
    /// </summary>
    public static DbContextOptionsBuilder Apply(
        DbContextOptionsBuilder options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                ArchitectureFixtureSchema.MigrationsHistoryTable,
                ArchitectureFixtureSchema.Name));
    }
}
