using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

internal sealed class IntegrationEventReceiptConfiguration : IEntityTypeConfiguration<IntegrationEventReceipt>
{
    public void Configure(EntityTypeBuilder<IntegrationEventReceipt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ArchitectureFixtureSchema.IntegrationEventReceiptTable, ArchitectureFixtureSchema.Name);
        builder.HasKey(receipt => receipt.EventId);

        builder.Property(receipt => receipt.EventId).HasColumnName("event_id");
        builder.Property(receipt => receipt.EventType).HasColumnName("event_type").HasMaxLength(200).IsRequired();
        builder.Property(receipt => receipt.ProbeId).HasColumnName("probe_id");
        builder.Property(receipt => receipt.ProbeLabel).HasColumnName("probe_label").HasMaxLength(100).IsRequired();
        builder.Property(receipt => receipt.ConsumedAtUtc).HasColumnName("consumed_at_utc").HasColumnType("timestamptz");
    }
}
