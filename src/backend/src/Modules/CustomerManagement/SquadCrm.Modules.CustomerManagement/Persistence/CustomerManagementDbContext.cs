using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.CustomerManagement.Persistence;

public sealed class CustomerManagementDbContext(DbContextOptions<CustomerManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(CustomerManagementSchema.Name);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customer");
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Id).HasColumnName("id");
            entity.Property(customer => customer.CustomerNumber).HasColumnName("customer_number").HasMaxLength(32);
            entity.HasIndex(customer => customer.CustomerNumber).IsUnique();
            entity.Property(customer => customer.FirstName).HasColumnName("first_name").HasMaxLength(200);
            entity.Property(customer => customer.LastName).HasColumnName("last_name").HasMaxLength(200);
            entity.Property(customer => customer.NormalizedFirstName).HasColumnName("normalized_first_name").HasMaxLength(200);
            entity.Property(customer => customer.NormalizedLastName).HasColumnName("normalized_last_name").HasMaxLength(200);
            entity.Property(customer => customer.PreferredLanguage).HasColumnName("preferred_language")
                .HasConversion<string>().HasMaxLength(16);
            entity.Property(customer => customer.DepartmentId).HasColumnName("department_id");
            entity.Property(customer => customer.BranchId).HasColumnName("branch_id");
            entity.Property(customer => customer.DepartmentMatchId).HasColumnName("department_match_id");
            entity.Property(customer => customer.BranchMatchId).HasColumnName("branch_match_id");
            entity.HasIndex(customer => new
            {
                customer.NormalizedFirstName,
                customer.NormalizedLastName,
                customer.DepartmentMatchId,
                customer.BranchMatchId,
            }).HasDatabaseName("ix_customer_duplicate_match").IsUnique();
            entity.Property(customer => customer.Status).HasColumnName("status")
                .HasConversion<string>().HasMaxLength(16);
            entity.Property(customer => customer.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(customer => customer.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });
    }
}
