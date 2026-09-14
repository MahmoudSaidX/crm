using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity.Presentation.Requests;

public sealed record CreateStaffUserRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required, MinLength(8), MaxLength(200)] string Password,
    [property: MaxLength(200)] string? DisplayName,
    [property: MaxLength(200)] string? Department,
    [property: MaxLength(200)] string? Branch);

public sealed record UpdateStaffUserRequest(
    [property: MaxLength(200)] string? DisplayName,
    [property: MaxLength(200)] string? Department,
    [property: MaxLength(200)] string? Branch);
