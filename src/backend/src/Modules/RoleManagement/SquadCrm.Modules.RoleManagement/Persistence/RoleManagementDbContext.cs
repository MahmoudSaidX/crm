using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class RoleManagementDbContext(DbContextOptions<RoleManagementDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAuditEvent> RoleAuditEvents => Set<RoleAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RoleManagementSchema.Name);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("role");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Id).HasColumnName("id");
            entity.Property(role => role.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(role => role.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
            entity.HasIndex(role => role.NormalizedName).IsUnique();
            entity.Property(role => role.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(role => role.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(role => role.NormalizedCode).IsUnique();
            entity.Property(role => role.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(role => role.IsActive).HasColumnName("is_active");
            entity.Property(role => role.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(role => role.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<RoleAuditEvent>(entity =>
        {
            entity.ToTable("role_audit_event");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.Id).HasColumnName("id");
            entity.Property(auditEvent => auditEvent.RoleId).HasColumnName("role_id");
            entity.Property(auditEvent => auditEvent.EventType).HasColumnName("event_type").HasMaxLength(32);
            entity.Property(auditEvent => auditEvent.ChangedByHandle).HasColumnName("changed_by_handle").HasMaxLength(256);
            entity.Property(auditEvent => auditEvent.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });
    }
}
