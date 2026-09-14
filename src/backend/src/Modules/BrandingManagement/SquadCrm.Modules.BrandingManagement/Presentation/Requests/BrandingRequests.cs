using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.BrandingManagement.Presentation.Requests;

public sealed record UpdateBrandingSettingsRequest(
    [property: Required, MaxLength(200)] string OrganizationDisplayNameEn,
    [property: MaxLength(200)] string? OrganizationDisplayNameAr,
    [property: Required, MaxLength(200)] string ProductDisplayNameEn,
    [property: MaxLength(200)] string? ProductDisplayNameAr,
    IReadOnlyDictionary<string, string>? ThemeTokens);
