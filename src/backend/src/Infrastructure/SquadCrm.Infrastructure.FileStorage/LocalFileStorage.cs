using SquadCrm.BuildingBlocks.Abstractions.Files;

namespace SquadCrm.Infrastructure.FileStorage;

public sealed class LocalFileStorage(
    FileStorageOptions options,
    IEnumerable<IFileUploadValidator> validators,
    TimeProvider timeProvider) : IFileStorage
{
    private readonly string _rootPath = EnsureRoot(options.LocalRootPath);
    private readonly string _temporaryPath = EnsureTemporaryRoot(options.LocalRootPath);

    public async Task<FileReference> UploadAsync(
        FileUpload upload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        foreach (IFileUploadValidator validator in validators)
        {
            await validator.ValidateAsync(upload, cancellationToken);
        }

        string storageKey = Guid.NewGuid().ToString("N");
        string destinationPath = ResolveStoragePath(storageKey);
        string temporaryFile = Path.Combine(_temporaryPath, $"{storageKey}.upload");

        try
        {
            await using (FileStream destination = new(
                temporaryFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                byte[] buffer = new byte[81920];
                long copied = 0;

                while (true)
                {
                    int read = await upload.Content.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    copied += read;
                    if (copied > upload.SizeBytes || copied > options.MaxSizeBytes)
                    {
                        throw new FileValidationException("File content exceeds its declared or configured size.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                if (copied != upload.SizeBytes)
                {
                    throw new FileValidationException("File content length does not match its declared size.");
                }
            }

            File.Move(temporaryFile, destinationPath);
        }
        catch
        {
            File.Delete(temporaryFile);
            throw;
        }

        return new FileReference(
            Guid.NewGuid(),
            storageKey,
            upload.OriginalFileName,
            upload.ContentType,
            upload.SizeBytes,
            timeProvider.GetUtcNow(),
            upload.CreatedBy);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = ResolveStoragePath(storageKey);

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(ResolveStoragePath(storageKey));
        return Task.CompletedTask;
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (!Guid.TryParseExact(storageKey, "N", out _))
        {
            throw new ArgumentException("Storage key is invalid.", nameof(storageKey));
        }

        string path = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        if (!path.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key resolves outside the configured root.", nameof(storageKey));
        }

        return path;
    }

    private static string EnsureRoot(string configuredPath)
    {
        string root = Path.GetFullPath(configuredPath);
        Directory.CreateDirectory(root);
        return Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
    }

    private static string EnsureTemporaryRoot(string configuredPath)
    {
        string path = Path.Combine(Path.GetFullPath(configuredPath), ".tmp");
        Directory.CreateDirectory(path);
        return path;
    }
}
