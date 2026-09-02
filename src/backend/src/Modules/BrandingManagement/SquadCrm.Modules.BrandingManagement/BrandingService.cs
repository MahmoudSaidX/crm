using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BrandingManagement.Persistence;

namespace SquadCrm.Modules.BrandingManagement;

public enum BrandingUpdateFailure
{
    None,
    InvalidThemeToken,
}

public readonly record struct BrandingUpdateResult(BrandingSetting? Setting, BrandingUpdateFailure Failure)
{
    public static BrandingUpdateResult Success(BrandingSetting setting) => new(setting, BrandingUpdateFailure.None);
    public static BrandingUpdateResult Failed(BrandingUpdateFailure failure) => new(null, failure);
}

internal sealed class BrandingService(
    BrandingManagementDbContext dbContext,
    IFileStorage fileStorage,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    TimeProvider timeProvider)
{
    private static readonly EffectiveBrandingResponse DefaultEffectiveBranding = new(
        OrganizationDisplayNameEn: "Squad CRM",
        OrganizationDisplayNameAr: null,
        ProductDisplayNameEn: "Squad CRM",
        ProductDisplayNameAr: null,
        ThemeTokens: new Dictionary<string, string>(),
        PrimaryLogoUrl: null,
        CompactLogoUrl: null,
        FaviconUrl: null,
        IsDefault: true);

    public async Task<BrandingSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        BrandingSetting setting = await GetOrCreateSettingAsync(cancellationToken);
        List<BrandingAsset> assets = await dbContext.BrandingAssets.AsNoTracking().ToListAsync(cancellationToken);
        return ToSettingsResponse(setting, assets);
    }

    public async Task<EffectiveBrandingResponse> GetEffectiveAsync(CancellationToken cancellationToken)
    {
        BrandingSetting? setting = await dbContext.BrandingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == BrandingSetting.SingletonId, cancellationToken);
        if (setting is null)
        {
            return DefaultEffectiveBranding;
        }

        Dictionary<string, string> assetUrlsByKind = await dbContext.BrandingAssets
            .AsNoTracking()
            .ToDictionaryAsync(
                asset => asset.Kind.ToString(),
                asset => $"/api/v1/branding/logo/{asset.Kind.ToString().ToLowerInvariant()}",
                cancellationToken);

        return new EffectiveBrandingResponse(
            setting.OrganizationDisplayNameEn,
            setting.OrganizationDisplayNameAr,
            setting.ProductDisplayNameEn,
            setting.ProductDisplayNameAr,
            DeserializeThemeTokens(setting.ThemeTokensJson),
            assetUrlsByKind.GetValueOrDefault(nameof(BrandingAssetKind.Primary)),
            assetUrlsByKind.GetValueOrDefault(nameof(BrandingAssetKind.Compact)),
            assetUrlsByKind.GetValueOrDefault(nameof(BrandingAssetKind.Favicon)),
            IsDefault: false);
    }

    public async Task<BrandingUpdateResult> UpdateAsync(UpdateBrandingSettingsRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string> themeTokens = request.ThemeTokens is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.ThemeTokens);
        foreach ((string key, string value) in themeTokens)
        {
            if (!ThemeTokenCatalog.AllowedKeys.Contains(key) || !ThemeTokenCatalog.IsValidValue(value))
            {
                return BrandingUpdateResult.Failed(BrandingUpdateFailure.InvalidThemeToken);
            }
        }

        BrandingSetting setting = await GetOrCreateSettingAsync(cancellationToken);
        setting.OrganizationDisplayNameEn = request.OrganizationDisplayNameEn.Trim();
        setting.OrganizationDisplayNameAr = NormalizeOptional(request.OrganizationDisplayNameAr);
        setting.ProductDisplayNameEn = request.ProductDisplayNameEn.Trim();
        setting.ProductDisplayNameAr = NormalizeOptional(request.ProductDisplayNameAr);
        setting.ThemeTokensJson = JsonSerializer.Serialize(themeTokens);
        setting.UpdatedAtUtc = timeProvider.GetUtcNow();
        setting.UpdatedByHandle = currentUserAccessor.Handle;

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync("updated", cancellationToken);
        return BrandingUpdateResult.Success(setting);
    }

    public async Task<BrandingSettingsResponse> UploadLogoAsync(
        BrandingAssetKind kind, FileUpload upload, CancellationToken cancellationToken)
    {
        FileReference uploaded = await fileStorage.UploadAsync(upload, cancellationToken);

        BrandingAsset? existing = await dbContext.BrandingAssets
            .SingleOrDefaultAsync(asset => asset.Kind == kind, cancellationToken);
        string? previousStorageKey = existing?.StorageKey;

        if (existing is null)
        {
            dbContext.BrandingAssets.Add(new BrandingAsset
            {
                Kind = kind,
                StorageKey = uploaded.StorageKey,
                ContentType = uploaded.ContentType,
                OriginalFileName = uploaded.OriginalFileName,
                SizeBytes = uploaded.SizeBytes,
                CreatedAtUtc = uploaded.CreatedAtUtc,
                CreatedBy = uploaded.CreatedBy,
            });
        }
        else
        {
            existing.StorageKey = uploaded.StorageKey;
            existing.ContentType = uploaded.ContentType;
            existing.OriginalFileName = uploaded.OriginalFileName;
            existing.SizeBytes = uploaded.SizeBytes;
            existing.CreatedAtUtc = uploaded.CreatedAtUtc;
            existing.CreatedBy = uploaded.CreatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Delete the superseded file only after the new pointer row has
        // committed (storage retention rule from BR) — never the reverse.
        if (previousStorageKey is not null)
        {
            await fileStorage.DeleteAsync(previousStorageKey, cancellationToken);
        }

        await RecordAuditAsync($"logo.{kind.ToString().ToLowerInvariant()}.uploaded", cancellationToken);
        return await GetSettingsAsync(cancellationToken);
    }

    public async Task<bool> DeleteLogoAsync(BrandingAssetKind kind, CancellationToken cancellationToken)
    {
        BrandingAsset? existing = await dbContext.BrandingAssets
            .SingleOrDefaultAsync(asset => asset.Kind == kind, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        dbContext.BrandingAssets.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(existing.StorageKey, cancellationToken);
        await RecordAuditAsync($"logo.{kind.ToString().ToLowerInvariant()}.deleted", cancellationToken);
        return true;
    }

    public async Task<(Stream Content, string ContentType)?> OpenLogoAsync(
        BrandingAssetKind kind, CancellationToken cancellationToken)
    {
        BrandingAsset? asset = await dbContext.BrandingAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Kind == kind, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        Stream content = await fileStorage.OpenReadAsync(asset.StorageKey, cancellationToken);
        return (content, asset.ContentType);
    }

    private async Task<BrandingSetting> GetOrCreateSettingAsync(CancellationToken cancellationToken)
    {
        BrandingSetting? setting = await dbContext.BrandingSettings
            .SingleOrDefaultAsync(candidate => candidate.Id == BrandingSetting.SingletonId, cancellationToken);
        if (setting is not null)
        {
            return setting;
        }

        setting = new BrandingSetting
        {
            OrganizationDisplayNameEn = DefaultEffectiveBranding.OrganizationDisplayNameEn,
            ProductDisplayNameEn = DefaultEffectiveBranding.ProductDisplayNameEn,
            UpdatedAtUtc = timeProvider.GetUtcNow(),
        };
        dbContext.BrandingSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private Task RecordAuditAsync(string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown",
                action,
                "Branding",
                BrandingSetting.SingletonId.ToString(),
                Metadata: null),
            cancellationToken);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, string> DeserializeThemeTokens(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private static BrandingSettingsResponse ToSettingsResponse(BrandingSetting setting, List<BrandingAsset> assets) => new(
        setting.OrganizationDisplayNameEn,
        setting.OrganizationDisplayNameAr,
        setting.ProductDisplayNameEn,
        setting.ProductDisplayNameAr,
        DeserializeThemeTokens(setting.ThemeTokensJson),
        assets.Select(asset => new BrandingAssetResponse(
            asset.Kind.ToString(), asset.OriginalFileName, asset.ContentType, asset.SizeBytes, asset.CreatedAtUtc)).ToList(),
        setting.UpdatedAtUtc,
        setting.UpdatedByHandle);
}
