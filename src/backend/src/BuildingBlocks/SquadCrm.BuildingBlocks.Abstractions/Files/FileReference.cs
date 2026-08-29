namespace SquadCrm.BuildingBlocks.Abstractions.Files;

/// <summary>
/// Provider-neutral metadata persisted by the business module that owns the
/// attachment. File bytes remain in <see cref="IFileStorage"/>.
/// </summary>
public sealed record FileReference(
    Guid Id,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy);
