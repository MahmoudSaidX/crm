using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class RoleManagementDbContext(DbContextOptions<RoleManagementDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAuditEvent> RoleAuditEvents => Set<RoleAuditEvent>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<StaffSubjectRole> StaffSubjectRoles => Set<StaffSubjectRole>();
    public DbSet<PermissionChangeAuditEvent> PermissionChangeAuditEvents => Set<PermissionChangeAuditEvent>();
    public DbSet<StaffRoleAssignmentAuditEvent> StaffRoleAssignmentAuditEvents => Set<StaffRoleAssignmentAuditEvent>();

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

        modelBuilder.Entity<PermissionDefinition>(entity =>
        {
            entity.ToTable("permission_definition");
            entity.HasKey(permission => permission.Code);
            entity.Property(permission => permission.Code).HasColumnName("code").HasMaxLength(128);
            entity.Property(permission => permission.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(permission => permission.Module).HasColumnName("module").HasMaxLength(100);
            entity.Property(permission => permission.Description).HasColumnName("description").HasMaxLength(1000);
            entity.HasData(
                new PermissionDefinition
                {
                    Code = Permissions.RolesView,
                    Name = "View roles",
                    Module = "Role Management",
                    Description = "View global roles and their assigned permissions.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.RolesManage,
                    Name = "Manage roles",
                    Module = "Role Management",
                    Description = "Create, update, activate, deactivate, and configure global roles.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.UsersView,
                    Name = "View staff users",
                    Module = "Staff Management",
                    Description = "View staff user profiles and their role assignments.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.UsersManage,
                    Name = "Manage staff users",
                    Module = "Staff Management",
                    Description = "Create, update, activate, deactivate staff users, and assign roles.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.AuditView,
                    Name = "View audit records",
                    Module = "Audit",
                    Description = "View the audit trail of administrative actions.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.DepartmentsView,
                    Name = "View departments",
                    Module = "Department Management",
                    Description = "View departments and their active state.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.DepartmentsManage,
                    Name = "Manage departments",
                    Module = "Department Management",
                    Description = "Create, update, activate and deactivate departments.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.BranchesView,
                    Name = "View branches",
                    Module = "Branch Management",
                    Description = "View branches and their active state.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.BranchesManage,
                    Name = "Manage branches",
                    Module = "Branch Management",
                    Description = "Create, update, activate and deactivate branches.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.BrandingView,
                    Name = "View branding",
                    Module = "Branding Management",
                    Description = "View organization/product branding configuration.",
                },
                new PermissionDefinition
                {
                    Code = Permissions.BrandingManage,
                    Name = "Manage branding",
                    Module = "Branding Management",
                    Description = "Update organization/product branding, logos and theme settings.",
                });
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permission");
            entity.HasKey(item => new { item.RoleId, item.PermissionCode });
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.Property(item => item.PermissionCode).HasColumnName("permission_code").HasMaxLength(128);
            entity.HasOne(item => item.Role).WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Permission).WithMany().HasForeignKey(item => item.PermissionCode).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StaffSubjectRole>(entity =>
        {
            entity.ToTable("staff_subject_role");
            entity.HasKey(item => new { item.StaffSubjectId, item.RoleId });
            entity.Property(item => item.StaffSubjectId).HasColumnName("staff_subject_id");
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.HasOne(item => item.Role).WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PermissionChangeAuditEvent>(entity =>
        {
            entity.ToTable("permission_change_audit_event");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.RoleId).HasColumnName("role_id");
            entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(64);
            entity.Property(item => item.PermissionCodes).HasColumnName("permission_codes").HasMaxLength(2048);
            entity.Property(item => item.ChangedByHandle).HasColumnName("changed_by_handle").HasMaxLength(256);
            entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });

        modelBuilder.Entity<StaffRoleAssignmentAuditEvent>(entity =>
        {
            entity.ToTable("staff_role_assignment_audit_event");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.StaffSubjectId).HasColumnName("staff_subject_id");
            entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(64);
            entity.Property(item => item.RoleCodes).HasColumnName("role_codes").HasMaxLength(2048);
            entity.Property(item => item.ChangedByHandle).HasColumnName("changed_by_handle").HasMaxLength(256);
            entity.Property(item => item.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });
    }
}
