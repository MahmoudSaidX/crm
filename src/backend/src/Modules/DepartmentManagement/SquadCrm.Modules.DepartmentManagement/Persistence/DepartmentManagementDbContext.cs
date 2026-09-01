using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.DepartmentManagement.Persistence;

public sealed class DepartmentManagementDbContext(DbContextOptions<DepartmentManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DepartmentManagementSchema.Name);

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("department");
            entity.HasKey(department => department.Id);
            entity.Property(department => department.Id).HasColumnName("id");
            entity.Property(department => department.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(department => department.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(department => department.NormalizedCode).IsUnique();
            entity.Property(department => department.ArabicName).HasColumnName("arabic_name").HasMaxLength(200);
            entity.Property(department => department.EnglishName).HasColumnName("english_name").HasMaxLength(200);
            entity.Property(department => department.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(department => department.IsActive).HasColumnName("is_active");
            entity.Property(department => department.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(department => department.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
