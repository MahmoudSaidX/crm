namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public required string PermissionCode { get; set; }
    public Role Role { get; set; } = null!;
    public PermissionDefinition Permission { get; set; } = null!;
}
