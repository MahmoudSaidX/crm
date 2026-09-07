using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public sealed class TicketManagementDbContext(DbContextOptions<TicketManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(TicketManagementSchema.Name);

        modelBuilder.Entity<TicketCategory>(entity =>
        {
            entity.ToTable("ticket_category");
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Id).HasColumnName("id");
            entity.Property(category => category.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(category => category.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(category => category.NormalizedCode).IsUnique();
            entity.Property(category => category.ArabicName).HasColumnName("arabic_name").HasMaxLength(200);
            entity.Property(category => category.EnglishName).HasColumnName("english_name").HasMaxLength(200);
            entity.Property(category => category.DefaultDepartmentId).HasColumnName("default_department_id");
            entity.Property(category => category.SortOrder).HasColumnName("sort_order");
            entity.Property(category => category.IsActive).HasColumnName("is_active");
            entity.Property(category => category.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(category => category.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<TicketPriority>(entity =>
        {
            entity.ToTable("ticket_priority");
            entity.HasKey(priority => priority.Id);
            entity.Property(priority => priority.Id).HasColumnName("id");
            entity.Property(priority => priority.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(priority => priority.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(priority => priority.NormalizedCode).IsUnique();
            entity.Property(priority => priority.ArabicName).HasColumnName("arabic_name").HasMaxLength(200);
            entity.Property(priority => priority.EnglishName).HasColumnName("english_name").HasMaxLength(200);
            entity.Property(priority => priority.Rank).HasColumnName("rank");
            entity.Property(priority => priority.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(priority => priority.IsActive).HasColumnName("is_active");
            entity.Property(priority => priority.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(priority => priority.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
