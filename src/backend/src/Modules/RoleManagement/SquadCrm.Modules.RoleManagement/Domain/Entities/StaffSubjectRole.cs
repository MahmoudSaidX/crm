namespace SquadCrm.Modules.RoleManagement.Domain.Entities;

public sealed class StaffSubjectRole
{
    public Guid StaffSubjectId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
