using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Explicit lowercase snake_case mapping. The names are written out rather than
/// derived by a naming-conventions package: a three-column scaffolding table is
/// not a reason to add a solution-wide dependency.
/// </summary>
internal sealed class PersistenceProbeConfiguration : IEntityTypeConfiguration<PersistenceProbe>
{
    public void Configure(EntityTypeBuilder<PersistenceProbe> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ArchitectureFixtureSchema.ProbeTable, ArchitectureFixtureSchema.Name);

        builder.HasKey(probe => probe.Id);

        builder.Property(probe => probe.Id)
            .HasColumnName("id");

        builder.Property(probe => probe.Label)
            .HasColumnName("label")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(probe => probe.RecordedAtUtc)
            .HasColumnName("recorded_at_utc")
            .HasColumnType("timestamptz");

        // DomainEvents is an in-memory-only collection (AC 1); it must never
        // be mapped as a column/navigation, or EF's model build fails (B6).
        builder.Ignore(probe => probe.DomainEvents);

        // No foreign key of any kind: cross-schema/cross-module foreign keys are
        // not the modular-monolith integration mechanism, and there is nothing
        // else for scaffolding to reference.
    }
}
