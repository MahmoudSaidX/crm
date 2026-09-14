namespace SquadCrm.Modules.RoleManagement.Domain.Entities;

public sealed class StaffRoleAssignmentAuditEvent
{
    public long Id { get; set; }
    public Guid StaffSubjectId { get; set; }
    public required string EventType { get; set; }
    public required string RoleCodes { get; set; }
    public string? ChangedByHandle { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
