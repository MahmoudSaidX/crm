using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    [Required]
    public string Issuer { get; init; } = "SquadCrm";

    [Required]
    public string Audience { get; init; } = "SquadCrm.Agent";

    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 15)]
    public int AccessTokenMinutes { get; init; } = 5;

    [Range(1, 30)]
    public int RefreshSessionDays { get; init; } = 7;

    [Range(1, 90)]
    public int RememberedSessionDays { get; init; } = 30;
}
