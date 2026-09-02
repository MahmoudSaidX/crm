using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.BrandingManagement;

public sealed record UpdateBrandingSettingsRequest(
    [property: Required, MaxLength(200)] string OrganizationDisplayNameEn,
    [property: MaxLength(200)] string? OrganizationDisplayNameAr,
    [property: Required, MaxLength(200)] string ProductDisplayNameEn,
    [property: MaxLength(200)] string? ProductDisplayNameAr,
    IReadOnlyDictionary<string, string>? ThemeTokens);

public sealed record BrandingAssetResponse(
    string Kind,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc);

/// <summary>Full admin view — see <see cref="EffectiveBrandingResponse"/> for the public-safe projection.</summary>
public sealed record BrandingSettingsResponse(
    string OrganizationDisplayNameEn,
    string? OrganizationDisplayNameAr,
    string ProductDisplayNameEn,
    string? ProductDisplayNameAr,
    IReadOnlyDictionary<string, string> ThemeTokens,
    IReadOnlyList<BrandingAssetResponse> Assets,
    DateTimeOffset UpdatedAtUtc,
    string? UpdatedByHandle);

/// <summary>
/// Anonymous, safe-default-backed projection consumed by both app shells
/// before/without authentication (BR: invalid/inaccessible branding must
/// fall back to a safe default without breaking the shell).
/// </summary>
public sealed record EffectiveBrandingResponse(
    string OrganizationDisplayNameEn,
    string? OrganizationDisplayNameAr,
    string ProductDisplayNameEn,
    string? ProductDisplayNameAr,
    IReadOnlyDictionary<string, string> ThemeTokens,
    string? PrimaryLogoUrl,
    string? CompactLogoUrl,
    string? FaviconUrl,
    bool IsDefault);
