using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public sealed class TicketManagementDbContext(DbContextOptions<TicketManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<TicketPriority> TicketPriorities => Set<TicketPriority>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Append-only ownership-change log (CRM-136).</summary>
    public DbSet<TicketAssignmentHistory> TicketAssignmentHistory => Set<TicketAssignmentHistory>();

    /// <summary>Append-only lifecycle status-change log (CRM-137).</summary>
    public DbSet<TicketStatusHistory> TicketStatusHistory => Set<TicketStatusHistory>();

    /// <summary>Append-only escalation log (CRM-138).</summary>
    public DbSet<TicketEscalationHistory> TicketEscalationHistory => Set<TicketEscalationHistory>();

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
            entity.Property(ticket => ticket.EscalationLevel).HasColumnName("escalation_level");
            entity.Property(ticket => ticket.EscalationTargetType)
                .HasColumnName("escalation_target_type").HasConversion<string>().HasMaxLength(32);
            entity.Property(ticket => ticket.EscalationTargetId).HasColumnName("escalation_target_id");
            entity.Property(ticket => ticket.EscalatedAtUtc).HasColumnName("escalated_at_utc");
            entity.Property(ticket => ticket.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(ticket => ticket.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(ticket => ticket.Version).HasColumnName("version").IsConcurrencyToken();

            // Domain events are a runtime-only concern, never persisted.
            entity.Ignore(ticket => ticket.DomainEvents);
        });

        modelBuilder.Entity<TicketAssignmentHistory>(entity =>
        {
            entity.ToTable("ticket_assignment_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.TicketId).HasColumnName("ticket_id");
            entity.Property(history => history.PreviousAgentId).HasColumnName("previous_agent_id");
            entity.Property(history => history.NewAgentId).HasColumnName("new_agent_id");
            entity.Property(history => history.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(history => history.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.ChangedBy).HasColumnName("changed_by").HasMaxLength(256);
            entity.Property(history => history.ChangedAtUtc).HasColumnName("changed_at_utc");

            // Read order for a single ticket's history (CRM-139 consumes it).
            entity.HasIndex(history => new { history.TicketId, history.ChangedAtUtc });
        });

        modelBuilder.Entity<TicketStatusHistory>(entity =>
        {
            entity.ToTable("ticket_status_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.TicketId).HasColumnName("ticket_id");
            entity.Property(history => history.PreviousStatus)
                .HasColumnName("previous_status").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.NewStatus)
                .HasColumnName("new_status").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(history => history.ChangedBy).HasColumnName("changed_by").HasMaxLength(256);
            entity.Property(history => history.ChangedAtUtc).HasColumnName("changed_at_utc");

            // Read order for a single ticket's history (CRM-139 consumes it).
            entity.HasIndex(history => new { history.TicketId, history.ChangedAtUtc });
        });

        modelBuilder.Entity<TicketEscalationHistory>(entity =>
        {
            entity.ToTable("ticket_escalation_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.TicketId).HasColumnName("ticket_id");
            entity.Property(history => history.PreviousLevel).HasColumnName("previous_level");
            entity.Property(history => history.NewLevel).HasColumnName("new_level");
            entity.Property(history => history.TargetType)
                .HasColumnName("target_type").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.TargetId).HasColumnName("target_id");
            entity.Property(history => history.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(history => history.Source).HasColumnName("source").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.EscalatedBy).HasColumnName("escalated_by").HasMaxLength(256);
            entity.Property(history => history.EscalatedAtUtc).HasColumnName("escalated_at_utc");

            // Read order for a single ticket's history (CRM-139 consumes it).
            entity.HasIndex(history => new { history.TicketId, history.EscalatedAtUtc });
        });

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
