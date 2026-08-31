namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class PermissionChangeAuditEvent
{
    public long Id { get; set; }
    public Guid RoleId { get; set; }
    public required string EventType { get; set; }
    public required string PermissionCodes { get; set; }
    public string? ChangedByHandle { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
