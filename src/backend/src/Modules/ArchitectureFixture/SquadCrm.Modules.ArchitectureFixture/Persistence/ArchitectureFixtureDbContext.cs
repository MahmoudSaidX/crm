using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// <b>Architecture scaffolding — not a CRM concept.</b> The
/// <c>ArchitectureFixture</c> module's own <see cref="DbContext"/>, proving the
/// schema-per-module pattern every later module follows: one context per module,
/// inside the module's implementation project, owning one PostgreSQL schema.
/// <para>
/// This context maps <b>only</b> this module's model. It must never expose
/// another module's <see cref="DbSet{TEntity}"/> or entity type, and must never
/// read or write another module's schema — not by EF mapping, not by raw SQL and
/// not through a view. Cross-module workflows use public contracts and events.
/// </para>
/// <para>
/// There is deliberately no shared application-wide <c>SquadCrmDbContext</c>;
/// <c>SquadCrm.ArchitectureTests</c> fails the build if one appears, or if a
/// context is parked outside its owning module's <c>Persistence</c> namespace.
/// </para>
/// </summary>
public sealed class ArchitectureFixtureDbContext(DbContextOptions<ArchitectureFixtureDbContext> options)
    : DbContext(options)
{
    /// <summary>The single scaffolding table this module owns.</summary>
    public DbSet<PersistenceProbe> PersistenceProbes => Set<PersistenceProbe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Every table this module owns lives in its own schema — never `public`,
        // and never another module's schema.
        modelBuilder.HasDefaultSchema(ArchitectureFixtureSchema.Name);
        modelBuilder.ApplyConfiguration(new PersistenceProbeConfiguration());
    }
}
