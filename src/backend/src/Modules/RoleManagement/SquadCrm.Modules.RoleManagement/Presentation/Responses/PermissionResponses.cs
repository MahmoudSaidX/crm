namespace SquadCrm.Modules.RoleManagement.Presentation.Responses;

public sealed record PermissionResponse(
    string Code,
    string Name,
    string Module,
    string? Description,
    bool Granted);

public sealed record CurrentPermissionsResponse(IReadOnlyList<string> PermissionCodes);
