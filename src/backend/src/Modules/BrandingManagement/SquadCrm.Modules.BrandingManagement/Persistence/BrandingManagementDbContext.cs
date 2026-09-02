using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.BrandingManagement.Persistence;

public sealed class BrandingManagementDbContext(DbContextOptions<BrandingManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<BrandingSetting> BrandingSettings => Set<BrandingSetting>();
    public DbSet<BrandingAsset> BrandingAssets => Set<BrandingAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BrandingManagementSchema.Name);

        modelBuilder.Entity<BrandingSetting>(entity =>
        {
            entity.ToTable("branding_setting");
            entity.HasKey(setting => setting.Id);
            entity.Property(setting => setting.Id).HasColumnName("id");
            entity.Property(setting => setting.OrganizationDisplayNameEn)
                .HasColumnName("organization_display_name_en").HasMaxLength(200);
            entity.Property(setting => setting.OrganizationDisplayNameAr)
                .HasColumnName("organization_display_name_ar").HasMaxLength(200);
            entity.Property(setting => setting.ProductDisplayNameEn)
                .HasColumnName("product_display_name_en").HasMaxLength(200);
            entity.Property(setting => setting.ProductDisplayNameAr)
                .HasColumnName("product_display_name_ar").HasMaxLength(200);
            entity.Property(setting => setting.ThemeTokensJson).HasColumnName("theme_tokens_json").HasMaxLength(2000);
            entity.Property(setting => setting.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(setting => setting.UpdatedByHandle).HasColumnName("updated_by_handle").HasMaxLength(256);
        });

        modelBuilder.Entity<BrandingAsset>(entity =>
        {
            entity.ToTable("branding_asset");
            entity.HasKey(asset => asset.Kind);
            entity.Property(asset => asset.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32);
            entity.Property(asset => asset.StorageKey).HasColumnName("storage_key").HasMaxLength(64);
            entity.Property(asset => asset.ContentType).HasColumnName("content_type").HasMaxLength(200);
            entity.Property(asset => asset.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(256);
            entity.Property(asset => asset.SizeBytes).HasColumnName("size_bytes");
            entity.Property(asset => asset.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(asset => asset.CreatedBy).HasColumnName("created_by").HasMaxLength(256);
        });
    }
}
