using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.Infrastructure.FileStorage;

namespace SquadCrm.Api.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"squadcrm-files-{Guid.NewGuid():N}");

    [Fact]
    public async Task UploadReadAndDelete_UsesOpaqueKeyAndKeepsMetadataOutOfPath()
    {
        byte[] content = "safe content"u8.ToArray();
        LocalFileStorage storage = CreateStorage();

        FileReference reference = await storage.UploadAsync(new FileUpload(
            new MemoryStream(content),
            "../../customer-note.txt",
            "text/plain",
            content.Length,
            "user-1"));

        Assert.True(Guid.TryParseExact(reference.StorageKey, "N", out _));
        Assert.Equal("../../customer-note.txt", reference.OriginalFileName);
        Assert.True(File.Exists(Path.Combine(_root, reference.StorageKey)));
        Assert.False(File.Exists(Path.Combine(_root, "customer-note.txt")));

        await using Stream stored = await storage.OpenReadAsync(reference.StorageKey);
        using MemoryStream copy = new();
        await stored.CopyToAsync(copy);
        Assert.Equal(content, copy.ToArray());

        await storage.DeleteAsync(reference.StorageKey);
        await storage.DeleteAsync(reference.StorageKey);
        Assert.False(File.Exists(Path.Combine(_root, reference.StorageKey)));
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("not-a-key")]
    [InlineData("")]
    public async Task Read_RejectsNonOpaqueKeys(string key)
    {
        LocalFileStorage storage = CreateStorage();
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(key));
    }

    [Fact]
    public async Task Upload_RejectsDisallowedTypeBeforeWriting()
    {
        LocalFileStorage storage = CreateStorage();

        await Assert.ThrowsAsync<FileValidationException>(() => storage.UploadAsync(new FileUpload(
            new MemoryStream([1, 2, 3]),
            "payload.exe",
            "application/octet-stream",
            3,
            "user-1")));

        Assert.Empty(Directory.EnumerateFiles(_root));
    }

    [Fact]
    public async Task Upload_RemovesPartialFileWhenDeclaredSizeDoesNotMatch()
    {
        LocalFileStorage storage = CreateStorage();

        await Assert.ThrowsAsync<FileValidationException>(() => storage.UploadAsync(new FileUpload(
            new MemoryStream([1, 2, 3]),
            "note.txt",
            "text/plain",
            2,
            "user-1")));

        Assert.Empty(Directory.EnumerateFiles(_root));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private LocalFileStorage CreateStorage()
    {
        FileStorageOptions options = new()
        {
            LocalRootPath = _root,
            MaxSizeBytes = 1024,
            AllowedContentTypes = ["text/plain"],
        };

        IFileUploadValidator validator = new TestValidator(options);
        return new LocalFileStorage(options, [validator], TimeProvider.System);
    }

    private sealed class TestValidator(FileStorageOptions options) : IFileUploadValidator
    {
        public ValueTask ValidateAsync(FileUpload upload, CancellationToken cancellationToken = default)
        {
            if (upload.SizeBytes <= 0 || upload.SizeBytes > options.MaxSizeBytes
                || !options.AllowedContentTypes.Contains(upload.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new FileValidationException("Rejected by test policy.");
            }

            return ValueTask.CompletedTask;
        }
    }
}
