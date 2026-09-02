namespace SquadCrm.Modules.BrandingManagement.Persistence;

/// <summary>
/// Singleton branding configuration row. <see cref="SingletonId"/> is the
/// only row this table ever holds — presentation configuration only, never
/// authorization or domain identity (BR).
/// </summary>
public sealed class BrandingSetting
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-00000000b120");

    public Guid Id { get; set; } = SingletonId;
    public required string OrganizationDisplayNameEn { get; set; }
    public string? OrganizationDisplayNameAr { get; set; }
    public required string ProductDisplayNameEn { get; set; }
    public string? ProductDisplayNameAr { get; set; }

    /// <summary>Serialized allow-listed theme token map; see <see cref="ThemeTokenCatalog"/>.</summary>
    public string ThemeTokensJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string? UpdatedByHandle { get; set; }
}
