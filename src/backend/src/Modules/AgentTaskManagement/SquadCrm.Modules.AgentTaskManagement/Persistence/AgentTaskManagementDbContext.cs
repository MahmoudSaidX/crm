using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

public sealed class AgentTaskManagementDbContext(DbContextOptions<AgentTaskManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();

    /// <summary>This module's own transactional outbox table (CRM-143/ADR-005).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AgentTaskManagementSchema.Name);

        modelBuilder.Entity<AgentTask>(entity =>
        {
            entity.ToTable("agent_task");
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Id).HasColumnName("id");
            entity.Property(task => task.Title).HasColumnName("title").HasMaxLength(200);
            entity.Property(task => task.Details).HasColumnName("details").HasMaxLength(4000);
            entity.Property(task => task.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(task => task.TicketId).HasColumnName("ticket_id");
            entity.Property(task => task.CustomerId).HasColumnName("customer_id");
            entity.Property(task => task.DueAtUtc).HasColumnName("due_at_utc");
            entity.Property(task => task.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
            entity.Property(task => task.CompletedAtUtc).HasColumnName("completed_at_utc");
            entity.Property(task => task.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(task => task.UpdatedAtUtc).HasColumnName("updated_at_utc");
            entity.Property(task => task.Version).HasColumnName("version").IsConcurrencyToken();

            // "My Tasks" and general list filtering both read by owner and by
            // status/due date (CRM-143 AC).
            entity.HasIndex(task => task.OwnerUserId);
            entity.HasIndex(task => task.DueAtUtc);

            // Domain events are a runtime-only concern, never persisted.
            entity.Ignore(task => task.DomainEvents);
        });

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
