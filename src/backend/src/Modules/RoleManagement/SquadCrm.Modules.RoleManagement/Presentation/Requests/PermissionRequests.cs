using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement.Presentation.Requests;

public sealed record ReplaceRolePermissionsRequest(
    [property: Required] IReadOnlyList<string> PermissionCodes);
