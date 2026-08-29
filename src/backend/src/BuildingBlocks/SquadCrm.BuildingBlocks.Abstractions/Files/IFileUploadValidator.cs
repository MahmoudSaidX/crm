namespace SquadCrm.BuildingBlocks.Abstractions.Files;

/// <summary>Adds provider-neutral policy checks before any bytes are persisted.</summary>
public interface IFileUploadValidator
{
    ValueTask ValidateAsync(FileUpload upload, CancellationToken cancellationToken = default);
}
