using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BrandingManagement;
using SquadCrm.Modules.BrandingManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class BrandingManagementTests
{
    public BrandingManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task GetEffective_WithNoSettingsSaved_ReturnsSafeDefault()
    {
        await using BrandingManagementDbContext context = PostgresTestDatabase.CreateBrandingManagementContext();
        BrandingService service = CreateService(context, out _, "admin@example.test");

        EffectiveBrandingResponse effective = await service.GetEffectiveAsync(CancellationToken.None);

        Assert.True(effective.IsDefault);
        Assert.Equal("Squad CRM", effective.ProductDisplayNameEn);
        Assert.Null(effective.PrimaryLogoUrl);
    }

    [Fact]
    public async Task Update_Succeeds_AndRecordsOneUpdatedAuditEntry()
    {
        await using BrandingManagementDbContext context = PostgresTestDatabase.CreateBrandingManagementContext();
        BrandingService service = CreateService(context, out RecordingAuditRecorder auditRecorder, "admin@example.test");

        BrandingUpdateResult result = await service.UpdateAsync(
            new UpdateBrandingSettingsRequest("Acme", "أكمي", "Acme CRM", null, new Dictionary<string, string> { ["primaryColor"] = "#112233" }),
            CancellationToken.None);

        Assert.Equal(BrandingUpdateFailure.None, result.Failure);
        Assert.Equal("Acme CRM", result.Setting!.ProductDisplayNameEn);
        Assert.Contains(auditRecorder.Requests, request => request.Action == "updated" && request.ActorHandle == "admin@example.test");

        EffectiveBrandingResponse effective = await service.GetEffectiveAsync(CancellationToken.None);
        Assert.False(effective.IsDefault);
        Assert.Equal("Acme CRM", effective.ProductDisplayNameEn);
        Assert.Equal("#112233", effective.ThemeTokens["primaryColor"]);
    }

    [Fact]
    public async Task Update_WithNonAllowListedThemeToken_IsRejected()
    {
        await using BrandingManagementDbContext context = PostgresTestDatabase.CreateBrandingManagementContext();
        BrandingService service = CreateService(context, out _, "admin@example.test");

        BrandingUpdateResult result = await service.UpdateAsync(
            new UpdateBrandingSettingsRequest("Acme", null, "Acme CRM", null, new Dictionary<string, string> { ["customCss"] = "body{}" }),
            CancellationToken.None);

        Assert.Equal(BrandingUpdateFailure.InvalidThemeToken, result.Failure);
    }

    [Fact]
    public async Task UploadLogo_ThenReplace_DeletesThePreviousStorageFile_AndRecordsAudit()
    {
        await using BrandingManagementDbContext context = PostgresTestDatabase.CreateBrandingManagementContext();
        BrandingService service = CreateService(context, out RecordingAuditRecorder auditRecorder, "admin@example.test", out InMemoryFileStorage storage);

        BrandingSettingsResponse afterFirst = await service.UploadLogoAsync(
            BrandingAssetKind.Primary, CreateUpload("logo1.png"), CancellationToken.None);
        Assert.Single(afterFirst.Assets, asset => asset.Kind == nameof(BrandingAssetKind.Primary));
        Assert.Single(storage.StoredKeys);

        BrandingSettingsResponse afterSecond = await service.UploadLogoAsync(
            BrandingAssetKind.Primary, CreateUpload("logo2.png"), CancellationToken.None);

        Assert.Single(afterSecond.Assets, asset => asset.Kind == nameof(BrandingAssetKind.Primary) && asset.OriginalFileName == "logo2.png");
        Assert.Single(storage.StoredKeys); // the superseded file was deleted, never orphaned
        Assert.Contains(auditRecorder.Requests, request => request.Action == "logo.primary.uploaded");
    }

    [Fact]
    public async Task DeleteLogo_RemovesRowAndStorageFile_AndUnknownKindReturnsFalse()
    {
        await using BrandingManagementDbContext context = PostgresTestDatabase.CreateBrandingManagementContext();
        BrandingService service = CreateService(context, out _, "admin@example.test", out InMemoryFileStorage storage);
        await service.UploadLogoAsync(BrandingAssetKind.Favicon, CreateUpload("favicon.png"), CancellationToken.None);

        bool deleted = await service.DeleteLogoAsync(BrandingAssetKind.Favicon, CancellationToken.None);
        bool deletedAgain = await service.DeleteLogoAsync(BrandingAssetKind.Favicon, CancellationToken.None);

        Assert.True(deleted);
        Assert.False(deletedAgain);
        Assert.Empty(storage.StoredKeys);
        Assert.Null(await service.OpenLogoAsync(BrandingAssetKind.Favicon, CancellationToken.None));
    }

    private static FileUpload CreateUpload(string fileName) =>
        new(new MemoryStream([1, 2, 3]), fileName, "image/png", 3, "admin@example.test");

    private static BrandingService CreateService(
        BrandingManagementDbContext context, out RecordingAuditRecorder auditRecorder, string? handle)
    {
        BrandingService service = CreateService(context, out RecordingAuditRecorder recorder, handle, out _);
        auditRecorder = recorder;
        return service;
    }

    private static BrandingService CreateService(
        BrandingManagementDbContext context, out RecordingAuditRecorder auditRecorder, string? handle, out InMemoryFileStorage storage)
    {
        auditRecorder = new RecordingAuditRecorder();
        storage = new InMemoryFileStorage();
        return new BrandingService(
            context, storage, new StubCurrentUserAccessor(handle), auditRecorder, TimeProvider.System);
    }

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public List<AuditRecordRequest> Requests { get; } = [];

        public Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = [];

        public IReadOnlyCollection<string> StoredKeys => _files.Keys;

        public async Task<FileReference> UploadAsync(FileUpload upload, CancellationToken cancellationToken = default)
        {
            using MemoryStream buffer = new();
            await upload.Content.CopyToAsync(buffer, cancellationToken);
            string storageKey = Guid.NewGuid().ToString("N");
            _files[storageKey] = buffer.ToArray();
            return new FileReference(
                Guid.NewGuid(), storageKey, upload.OriginalFileName, upload.ContentType, upload.SizeBytes,
                DateTimeOffset.UtcNow, upload.CreatedBy);
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_files[storageKey]));

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
