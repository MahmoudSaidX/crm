namespace SquadCrm.Modules.BrandingManagement.Persistence;

public enum BrandingAssetKind
{
    Primary,
    Compact,
    Favicon,
}

/// <summary>
/// One row per <see cref="BrandingAssetKind"/> (at most three total).
/// File bytes live in the shared <c>IFileStorage</c>; this row is the
/// provider-neutral pointer + display metadata (mirrors <c>FileReference</c>).
/// </summary>
public sealed class BrandingAsset
{
    public BrandingAssetKind Kind { get; set; }
    public required string StorageKey { get; set; }
    public required string ContentType { get; set; }
    public required string OriginalFileName { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedBy { get; set; }
}
