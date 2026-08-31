namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class PermissionDefinition
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Module { get; set; }
    public string? Description { get; set; }
}
