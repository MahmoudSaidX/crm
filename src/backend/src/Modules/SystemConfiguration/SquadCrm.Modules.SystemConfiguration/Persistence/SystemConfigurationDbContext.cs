using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.SystemConfiguration.Persistence;

public sealed class SystemConfigurationDbContext(DbContextOptions<SystemConfigurationDbContext> options)
    : DbContext(options)
{
    public DbSet<ConfigurationValue> ConfigurationValues => Set<ConfigurationValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SystemConfigurationSchema.Name);

        modelBuilder.Entity<ConfigurationValue>(entity =>
        {
            entity.ToTable("configuration_value");
            entity.HasKey(value => value.Key);
            entity.Property(value => value.Key).HasColumnName("key").HasMaxLength(128);
            entity.Property(value => value.Value).HasColumnName("value").HasMaxLength(2000);
            entity.Property(value => value.UpdatedByHandle).HasColumnName("updated_by_handle").HasMaxLength(256);
            entity.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
