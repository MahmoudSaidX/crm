using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity;

public sealed record SignInRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required, MinLength(8), MaxLength(256)] string Password,
    bool RememberSession = false);

public sealed record AccessCredentialResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record CurrentStaffResponse(Guid StaffUserId);
