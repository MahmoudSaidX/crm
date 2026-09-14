using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement.Presentation.Requests;

public sealed record CreateRoleRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(64)] string Code,
    [property: MaxLength(1000)] string? Description);

public sealed record UpdateRoleRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(64)] string Code,
    [property: MaxLength(1000)] string? Description);
