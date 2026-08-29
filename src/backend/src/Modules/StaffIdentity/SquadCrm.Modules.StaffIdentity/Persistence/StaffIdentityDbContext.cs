using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.StaffIdentity.Persistence;

public sealed class StaffIdentityDbContext(DbContextOptions<StaffIdentityDbContext> options) : DbContext(options)
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AuthenticationEvent> AuthenticationEvents => Set<AuthenticationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(StaffIdentitySchema.Name);

        modelBuilder.Entity<StaffUser>(entity =>
        {
            entity.ToTable("staff_user");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320);
            entity.HasIndex(user => user.NormalizedEmail).IsUnique();
            entity.Property(user => user.PasswordHash).HasColumnName("password_hash").HasMaxLength(1024);
            entity.Property(user => user.IsActive).HasColumnName("is_active");
            entity.Property(user => user.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("refresh_session");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.StaffUserId).HasColumnName("staff_user_id");
            entity.Property(session => session.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.Property(session => session.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(session => session.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(session => session.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(session => session.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
            entity.HasOne(session => session.StaffUser).WithMany(user => user.Sessions)
                .HasForeignKey(session => session.StaffUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthenticationEvent>(entity =>
        {
            entity.ToTable("authentication_event");
            entity.HasKey(authenticationEvent => authenticationEvent.Id);
            entity.Property(authenticationEvent => authenticationEvent.StaffUserId).HasColumnName("staff_user_id");
            entity.Property(authenticationEvent => authenticationEvent.EventType).HasColumnName("event_type").HasMaxLength(32);
            entity.Property(authenticationEvent => authenticationEvent.Outcome).HasColumnName("outcome").HasMaxLength(32);
            entity.Property(authenticationEvent => authenticationEvent.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });
    }
}
