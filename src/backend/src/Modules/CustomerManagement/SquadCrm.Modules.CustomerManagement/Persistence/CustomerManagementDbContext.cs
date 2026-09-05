using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.CustomerManagement.Persistence;

public sealed class CustomerManagementDbContext(DbContextOptions<CustomerManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(CustomerManagementSchema.Name);

        modelBuilder.Entity<CustomerContact>(entity =>
        {
            entity.ToTable("customer_contact");
            entity.HasKey(contact => contact.Id);
            entity.Property(contact => contact.Id).HasColumnName("id");
            entity.Property(contact => contact.CustomerId).HasColumnName("customer_id");
            entity.HasOne<Customer>().WithMany().HasForeignKey(contact => contact.CustomerId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(contact => contact.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
            entity.Property(contact => contact.Value).HasColumnName("value").HasMaxLength(320);
            entity.Property(contact => contact.NormalizedValue).HasColumnName("normalized_value").HasMaxLength(320);
            entity.Property(contact => contact.Label).HasColumnName("label").HasMaxLength(100);
            entity.Property(contact => contact.IsPrimary).HasColumnName("is_primary");
            entity.Property(contact => contact.IsActive).HasColumnName("is_active");
            entity.Property(contact => contact.VerifiedAtUtc).HasColumnName("verified_at_utc");
            entity.Property(contact => contact.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(contact => contact.UpdatedAtUtc).HasColumnName("updated_at_utc");

            // Enforces "at most one active primary contact per type per
            // customer" at the database layer; a partial index is required
            // because plain uniqueness would also forbid multiple inactive/
            // non-primary rows of the same type.
            entity.HasIndex(contact => new { contact.CustomerId, contact.Type })
                .HasDatabaseName("ix_customer_contact_active_primary")
                .IsUnique()
                .HasFilter("is_primary = true AND is_active = true");
        });

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

            // Optimistic concurrency via Postgres's native xmin system column
            // (no physical column/migration added) — required by CRM-125's
            // "conflicting concurrent edits return a clear conflict response".
            entity.Property(customer => customer.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });
    }
}
