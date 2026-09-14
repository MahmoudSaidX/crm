using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement.Presentation.Requests;

public sealed record ReplaceStaffRolesRequest(
    [property: Required] IReadOnlyList<Guid> RoleIds);
