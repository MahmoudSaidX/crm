using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.Audit.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditSchema.Name);

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("audit_record");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.ActorHandle).HasColumnName("actor_handle").HasMaxLength(256);
            entity.Property(record => record.Action).HasColumnName("action").HasMaxLength(128);
            entity.Property(record => record.EntityType).HasColumnName("entity_type").HasMaxLength(128);
            entity.Property(record => record.EntityId).HasColumnName("entity_id").HasMaxLength(128);
            entity.Property(record => record.MetadataJson).HasColumnName("metadata_json").HasMaxLength(4000);
            entity.Property(record => record.OccurredAtUtc).HasColumnName("occurred_at_utc");
            entity.HasIndex(record => new { record.EntityType, record.EntityId });
            entity.HasIndex(record => record.OccurredAtUtc);
        });
    }
}
