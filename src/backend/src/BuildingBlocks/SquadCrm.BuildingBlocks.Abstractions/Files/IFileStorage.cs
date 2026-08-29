namespace SquadCrm.BuildingBlocks.Abstractions.Files;

/// <summary>
/// Stores bytes behind opaque keys. Callers must authorize the owning business
/// resource before invoking read or delete; storage keys are not authorization tokens.
/// </summary>
public interface IFileStorage
{
    Task<FileReference> UploadAsync(FileUpload upload, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
