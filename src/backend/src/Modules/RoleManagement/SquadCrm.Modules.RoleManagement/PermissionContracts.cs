using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement;

public sealed record PermissionResponse(
    string Code,
    string Name,
    string Module,
    string? Description,
    bool Granted);

public sealed record CurrentPermissionsResponse(IReadOnlyList<string> PermissionCodes);

public sealed record ReplaceRolePermissionsRequest(
    [property: Required] IReadOnlyList<string> PermissionCodes);
