using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity.Presentation.Requests;

public sealed record SignInRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required, MinLength(8), MaxLength(256)] string Password,
    bool RememberSession = false);
