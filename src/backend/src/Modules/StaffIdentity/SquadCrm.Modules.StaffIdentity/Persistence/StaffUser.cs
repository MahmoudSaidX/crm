namespace SquadCrm.Modules.StaffIdentity.Persistence;

public sealed class StaffUser
{
    public Guid Id { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string PasswordHash { get; set; }
    public string? DisplayName { get; set; }
    public string? Department { get; set; }
    public string? Branch { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<RefreshSession> Sessions { get; } = [];
}
