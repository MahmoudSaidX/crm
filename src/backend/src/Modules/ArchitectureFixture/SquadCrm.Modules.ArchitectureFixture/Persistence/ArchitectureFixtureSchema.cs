namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// <b>Architecture scaffolding — not a CRM concept.</b> The PostgreSQL
/// identifiers this module owns exclusively.
/// <para>
/// The schema name is deliberately unmistakable as non-business. No module
/// table is ever placed in the PostgreSQL <c>public</c> schema, and no module
/// reads or writes another module's schema.
/// </para>
/// </summary>
internal static class ArchitectureFixtureSchema
{
    /// <summary>The PostgreSQL schema owned exclusively by this module.</summary>
    public const string Name = "architecture_fixture";

    /// <summary>
    /// This module's own EF migration-history table, placed inside the module
    /// schema. Several module DbContexts share one physical database, so a
    /// shared <c>public.__EFMigrationsHistory</c> would let one module's
    /// migrations be recorded against another's.
    /// </summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>The single scaffolding table. Not a CRM entity.</summary>
    public const string ProbeTable = "persistence_probe";
}
