namespace SquadCrm.Modules.RoleManagement.Domain.Entities;

public sealed class PermissionDefinition
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Module { get; set; }
    public string? Description { get; set; }
}
