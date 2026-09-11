using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

/// <summary>
/// Explicit lowercase snake_case mapping, matching
/// <c>TicketManagement.Persistence.OutboxMessageConfiguration</c>'s style.
/// </summary>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("agent_task_outbox_message");

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

        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(128)
            .IsRequired();

        // No foreign key: cross-schema/cross-module foreign keys are not the
        // modular-monolith integration mechanism.
    }
}
