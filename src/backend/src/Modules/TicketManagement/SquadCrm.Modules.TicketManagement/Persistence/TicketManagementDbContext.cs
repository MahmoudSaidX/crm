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

    /// <summary>Append-only internal collaboration notes (CRM-147).</summary>
    public DbSet<TicketInternalNote> TicketInternalNotes => Set<TicketInternalNote>();

    /// <summary>Validated staff mentions on internal notes (CRM-147).</summary>
    public DbSet<TicketNoteMention> TicketNoteMentions => Set<TicketNoteMention>();

    /// <summary>Current watcher membership set (CRM-147).</summary>
    public DbSet<TicketWatcher> TicketWatchers => Set<TicketWatcher>();

    /// <summary>Append-only watcher membership-change log (CRM-147).</summary>
    public DbSet<TicketWatcherHistory> TicketWatcherHistory => Set<TicketWatcherHistory>();

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

        modelBuilder.Entity<TicketInternalNote>(entity =>
        {
            entity.ToTable("ticket_internal_note");
            entity.HasKey(note => note.Id);
            entity.Property(note => note.Id).HasColumnName("id");
            entity.Property(note => note.TicketId).HasColumnName("ticket_id");
            entity.Property(note => note.Body).HasColumnName("body").HasMaxLength(4000);
            entity.Property(note => note.CreatedBy).HasColumnName("created_by").HasMaxLength(256);
            entity.Property(note => note.CreatedAtUtc).HasColumnName("created_at_utc");

            // Chronological read of one ticket's notes — the notes list and the
            // CRM-139 timeline arm both use exactly this order.
            entity.HasIndex(note => new { note.TicketId, note.CreatedAtUtc });

            // Domain events are a runtime-only concern, never persisted.
            entity.Ignore(note => note.DomainEvents);
        });

        modelBuilder.Entity<TicketNoteMention>(entity =>
        {
            entity.ToTable("ticket_note_mention");
            entity.HasKey(mention => mention.Id);
            entity.Property(mention => mention.Id).HasColumnName("id");
            entity.Property(mention => mention.NoteId).HasColumnName("note_id");
            entity.Property(mention => mention.TicketId).HasColumnName("ticket_id");
            entity.Property(mention => mention.MentionedUserId).HasColumnName("mentioned_user_id");
            entity.Property(mention => mention.CreatedAtUtc).HasColumnName("created_at_utc");

            // A note's mentions die with the note; nothing else references them.
            entity.HasOne<TicketInternalNote>()
                .WithMany()
                .HasForeignKey(mention => mention.NoteId)
                .OnDelete(DeleteBehavior.Cascade);

            // One mention per user per note: a request repeating the same id
            // cannot produce two rows and therefore two notifications.
            entity.HasIndex(mention => new { mention.NoteId, mention.MentionedUserId }).IsUnique();

            // "Which tickets was I mentioned on" — the read CRM-155 will need.
            entity.HasIndex(mention => new { mention.MentionedUserId, mention.CreatedAtUtc });
        });

        modelBuilder.Entity<TicketWatcher>(entity =>
        {
            entity.ToTable("ticket_watcher");
            entity.HasKey(watcher => watcher.Id);
            entity.Property(watcher => watcher.Id).HasColumnName("id");
            entity.Property(watcher => watcher.TicketId).HasColumnName("ticket_id");
            entity.Property(watcher => watcher.UserId).HasColumnName("user_id");
            entity.Property(watcher => watcher.AddedBy).HasColumnName("added_by").HasMaxLength(256);
            entity.Property(watcher => watcher.AddedAtUtc).HasColumnName("added_at_utc");

            // Membership is a set: the database, not just the service's
            // pre-check, rejects a duplicate under a concurrent double-add.
            entity.HasIndex(watcher => new { watcher.TicketId, watcher.UserId }).IsUnique();

            // Domain events are a runtime-only concern, never persisted.
            entity.Ignore(watcher => watcher.DomainEvents);
        });

        modelBuilder.Entity<TicketWatcherHistory>(entity =>
        {
            entity.ToTable("ticket_watcher_history");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Id).HasColumnName("id");
            entity.Property(history => history.TicketId).HasColumnName("ticket_id");
            entity.Property(history => history.UserId).HasColumnName("user_id");
            entity.Property(history => history.Action)
                .HasColumnName("action").HasConversion<string>().HasMaxLength(32);
            entity.Property(history => history.ChangedBy).HasColumnName("changed_by").HasMaxLength(256);
            entity.Property(history => history.ChangedAtUtc).HasColumnName("changed_at_utc");

            // Read order for a single ticket's history (CRM-139 consumes it).
            entity.HasIndex(history => new { history.TicketId, history.ChangedAtUtc });
        });

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
