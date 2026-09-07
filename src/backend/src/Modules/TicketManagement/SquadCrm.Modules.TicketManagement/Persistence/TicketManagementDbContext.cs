using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public sealed class TicketManagementDbContext(DbContextOptions<TicketManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>This module's own transactional outbox table (CRM-133/ADR-005).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("ticket");
            entity.HasKey(ticket => ticket.Id);
            entity.Property(ticket => ticket.Id).HasColumnName("id");
            entity.Property(ticket => ticket.TicketNumber).HasColumnName("ticket_number").HasMaxLength(64);
            entity.HasIndex(ticket => ticket.TicketNumber).IsUnique();
            entity.Property(ticket => ticket.CustomerId).HasColumnName("customer_id");
            entity.Property(ticket => ticket.Subject).HasColumnName("subject").HasMaxLength(200);
            entity.Property(ticket => ticket.Description).HasColumnName("description").HasMaxLength(4000);
            entity.Property(ticket => ticket.CategoryId).HasColumnName("category_id");
            entity.Property(ticket => ticket.SubcategoryId).HasColumnName("subcategory_id");
            entity.Property(ticket => ticket.PriorityId).HasColumnName("priority_id");
            entity.Property(ticket => ticket.DepartmentId).HasColumnName("department_id");
            entity.Property(ticket => ticket.BranchId).HasColumnName("branch_id");
            entity.Property(ticket => ticket.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.Channel).HasColumnName("channel").HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.AssignedAgentId).HasColumnName("assigned_agent_id");
            entity.Property(ticket => ticket.CreatedAtUtc).HasColumnName("created_at_utc");

            // Domain events are a runtime-only concern, never persisted.
            entity.Ignore(ticket => ticket.DomainEvents);
        });

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
