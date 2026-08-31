using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement;

public sealed record RoleSummary(Guid Id, string Name, string Code);

public sealed record ReplaceStaffRolesRequest(
    [property: Required] IReadOnlyList<Guid> RoleIds);
