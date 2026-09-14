using Microsoft.EntityFrameworkCore;

namespace SquadCrm.Modules.QuickReplyManagement.Persistence;

public sealed class QuickReplyManagementDbContext(DbContextOptions<QuickReplyManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<QuickReply> QuickReplies => Set<QuickReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(QuickReplyManagementSchema.Name);

        modelBuilder.Entity<QuickReply>(entity =>
        {
            entity.ToTable("quick_reply");
            entity.HasKey(quickReply => quickReply.Id);
            entity.Property(quickReply => quickReply.Id).HasColumnName("id");
            entity.Property(quickReply => quickReply.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(quickReply => quickReply.NormalizedName)
                .HasColumnName("normalized_name").HasMaxLength(200);
            entity.Property(quickReply => quickReply.ArabicContent)
                .HasColumnName("arabic_content").HasMaxLength(4000);
            entity.Property(quickReply => quickReply.EnglishContent)
                .HasColumnName("english_content").HasMaxLength(4000);
            entity.Property(quickReply => quickReply.Scope)
                .HasColumnName("scope").HasConversion<string>().HasMaxLength(32);
            entity.Property(quickReply => quickReply.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(quickReply => quickReply.IsActive).HasColumnName("is_active");
            entity.Property(quickReply => quickReply.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(quickReply => quickReply.UpdatedAtUtc).HasColumnName("updated_at_utc");

            // Name uniqueness is per scope (Fields Dictionary: "unique
            // according to scope policy"), and it takes TWO partial indexes
            // rather than one composite over (normalized_name, scope,
            // owner_user_id). Postgres treats NULLs as distinct in a unique
            // index, so a composite index would let an unlimited number of
            // Global rows (owner_user_id NULL) share one name — the exact case
            // that most needs to be unique.
            entity.HasIndex(quickReply => quickReply.NormalizedName)
                .HasDatabaseName("ux_quick_reply_global_normalized_name")
                .IsUnique()
                .HasFilter("scope = 'Global'");
            entity.HasIndex(quickReply => new { quickReply.NormalizedName, quickReply.OwnerUserId })
                .HasDatabaseName("ux_quick_reply_personal_owner_normalized_name")
                .IsUnique()
                .HasFilter("scope = 'Personal'");

            // Every list read is "Global templates plus my own", so owner is
            // the selective column.
            entity.HasIndex(quickReply => quickReply.OwnerUserId);
        });
    }
}
