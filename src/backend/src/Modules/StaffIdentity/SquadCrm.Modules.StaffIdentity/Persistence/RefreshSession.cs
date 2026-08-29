namespace SquadCrm.Modules.StaffIdentity.Persistence;

public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public StaffUser StaffUser { get; set; } = null!;
}
