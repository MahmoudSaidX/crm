using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.BranchManagement.Persistence;

public sealed class BranchManagementDbContext(DbContextOptions<BranchManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Branch> Branches => Set<Branch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BranchManagementSchema.Name);

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branch");
            entity.HasKey(branch => branch.Id);
            entity.Property(branch => branch.Id).HasColumnName("id");
            entity.Property(branch => branch.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(branch => branch.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(branch => branch.NormalizedCode).IsUnique();
            entity.Property(branch => branch.ArabicName).HasColumnName("arabic_name").HasMaxLength(200);
            entity.Property(branch => branch.EnglishName).HasColumnName("english_name").HasMaxLength(200);
            entity.Property(branch => branch.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(branch => branch.IsActive).HasColumnName("is_active");
            entity.Property(branch => branch.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(branch => branch.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
