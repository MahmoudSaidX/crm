using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity;

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

public sealed record StaffUserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    string? Department,
    string? Branch,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
