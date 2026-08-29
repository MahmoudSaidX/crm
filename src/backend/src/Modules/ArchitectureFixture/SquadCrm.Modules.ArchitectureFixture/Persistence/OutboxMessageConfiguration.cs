using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Explicit lowercase snake_case mapping, matching <see cref="PersistenceProbeConfiguration"/>'s
/// style exactly.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ArchitectureFixtureSchema.OutboxTable, ArchitectureFixtureSchema.Name);

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id");

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(message => message.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(message => message.RetryCount)
            .HasColumnName("retry_count");

        builder.Property(message => message.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(message => message.LeaseId)
            .HasColumnName("lease_id");

        builder.Property(message => message.LeasedUntilUtc)
            .HasColumnName("leased_until_utc")
            .HasColumnType("timestamptz");

        builder.Property(message => message.NextAttemptAtUtc)
            .HasColumnName("next_attempt_at_utc")
            .HasColumnType("timestamptz");

        // Accelerates the pending-row lookup a future story (CRM-199) will
        // need. No retention/purge story owns this table yet.
        builder.HasIndex(message => new { message.ProcessedAtUtc, message.NextAttemptAtUtc, message.OccurredAtUtc })
            .HasFilter("processed_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_message_pending");

        // No foreign key: cross-schema/cross-module foreign keys are not the
        // modular-monolith integration mechanism.
    }
}
