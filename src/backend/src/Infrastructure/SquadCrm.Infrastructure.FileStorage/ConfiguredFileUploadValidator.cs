using SquadCrm.BuildingBlocks.Abstractions.Files;

namespace SquadCrm.Infrastructure.FileStorage;

internal sealed class ConfiguredFileUploadValidator(FileStorageOptions options) : IFileUploadValidator
{
    private readonly HashSet<string> _allowedContentTypes = new(
        options.AllowedContentTypes,
        StringComparer.OrdinalIgnoreCase);

    public ValueTask ValidateAsync(FileUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        cancellationToken.ThrowIfCancellationRequested();

        if (!upload.Content.CanRead)
        {
            throw new FileValidationException("File content must be readable.");
        }

        if (string.IsNullOrWhiteSpace(upload.OriginalFileName)
            || upload.OriginalFileName.Any(char.IsControl))
        {
            throw new FileValidationException("Original filename is required and cannot contain control characters.");
        }

        if (string.IsNullOrWhiteSpace(upload.CreatedBy))
        {
            throw new FileValidationException("CreatedBy is required.");
        }

        if (upload.SizeBytes <= 0 || upload.SizeBytes > options.MaxSizeBytes)
        {
            throw new FileValidationException($"File size must be between 1 and {options.MaxSizeBytes} bytes.");
        }

        if (!_allowedContentTypes.Contains(upload.ContentType))
        {
            throw new FileValidationException("File content type is not allowed.");
        }

        return ValueTask.CompletedTask;
    }
}
