using System.Text;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerAttachmentManagementTests
{
    private const string UploaderHandle = "agent@example.test";

    public CustomerAttachmentManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Upload_ValidFile_PersistsMetadataAndRecordsAudit()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerAttachmentService service = CreateService(context, auditRecorder, out InMemoryFileStorage storage);

        CustomerAttachmentResult result = await service.UploadAsync(
            customerId, CreateUpload("contract.pdf"), "  Signed contract  ", CancellationToken.None);

        Assert.Equal(CustomerAttachmentFailure.None, result.Failure);
        Assert.Equal("contract.pdf", result.Attachment!.OriginalFileName);
        Assert.Equal("Signed contract", result.Attachment.Description);
        Assert.Equal(UploaderHandle, result.Attachment.UploadedBy);
        Assert.Single(storage.StoredKeys);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "attachment_added" && request.EntityId == result.Attachment.Id.ToString());
    }

    [Fact]
    public async Task Upload_UnknownCustomer_IsRejectedWithoutStoringBytes()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerAttachmentService service = CreateService(
            context, new RecordingAuditRecorder(), out InMemoryFileStorage storage);

        CustomerAttachmentResult result = await service.UploadAsync(
            Guid.NewGuid(), CreateUpload("orphan.pdf"), null, CancellationToken.None);

        Assert.Equal(CustomerAttachmentFailure.CustomerNotFound, result.Failure);
        Assert.Empty(storage.StoredKeys);
    }

    [Fact]
    public async Task Upload_RejectedByStorageValidation_PersistsNoMetadata()
    {
        // Size/type rules live in the storage abstraction's configured
        // validator (covered by LocalFileStorageTests); this asserts the
        // service surfaces the rejection instead of persisting a row.
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerAttachmentService service = new(
            context,
            new RejectingFileStorage(),
            new StaticCurrentUserAccessor(UploaderHandle),
            new RecordingAuditRecorder());

        await Assert.ThrowsAsync<FileValidationException>(() => service.UploadAsync(
            customerId,
            CreateUpload("payload.exe", contentType: "application/x-msdownload"),
            null,
            CancellationToken.None));

        Assert.Empty(await service.ListAsync(customerId, CancellationToken.None));
    }

    [Fact]
    public async Task List_ExcludesRemovedAttachments_AndIsChronological()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerAttachmentService service = CreateService(context, new RecordingAuditRecorder(), out _);
        CustomerAttachmentResult first = await service.UploadAsync(
            customerId, CreateUpload("first.pdf"), null, CancellationToken.None);
        CustomerAttachmentResult second = await service.UploadAsync(
            customerId, CreateUpload("second.pdf"), null, CancellationToken.None);
        CustomerAttachmentResult third = await service.UploadAsync(
            customerId, CreateUpload("third.pdf"), null, CancellationToken.None);

        await service.RemoveAsync(customerId, second.Attachment!.Id, CancellationToken.None);
        List<CustomerAttachment> attachments = await service.ListAsync(customerId, CancellationToken.None);

        Assert.Equal(2, attachments.Count);
        Assert.Equal(first.Attachment!.Id, attachments[0].Id);
        Assert.Equal(third.Attachment!.Id, attachments[1].Id);
    }

    [Fact]
    public async Task Remove_KeepsRowAndStoredBytes_ForAuditRetention()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerAttachmentService service = CreateService(context, auditRecorder, out InMemoryFileStorage storage);
        CustomerAttachmentResult uploaded = await service.UploadAsync(
            customerId, CreateUpload("retained.pdf"), null, CancellationToken.None);

        CustomerAttachmentResult removed = await service.RemoveAsync(
            customerId, uploaded.Attachment!.Id, CancellationToken.None);
        CustomerAttachmentResult removedAgain = await service.RemoveAsync(
            customerId, uploaded.Attachment.Id, CancellationToken.None);

        Assert.Equal(CustomerAttachmentFailure.None, removed.Failure);
        Assert.NotNull(removed.Attachment!.RemovedAtUtc);
        Assert.Equal(UploaderHandle, removed.Attachment.RemovedBy);
        Assert.Equal(CustomerAttachmentFailure.AttachmentNotFound, removedAgain.Failure);
        Assert.Single(storage.StoredKeys);
        Assert.Single(auditRecorder.Requests, request => request.Action == "attachment_removed");
    }

    [Fact]
    public async Task Open_ReturnsContent_ForOwningCustomerOnly()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        Guid otherCustomerId = await CreateCustomerAsync(context);
        CustomerAttachmentService service = CreateService(context, new RecordingAuditRecorder(), out _);
        CustomerAttachmentResult uploaded = await service.UploadAsync(
            customerId, CreateUpload("readable.txt", contentType: "text/plain"), null, CancellationToken.None);

        CustomerAttachmentContent? owned = await service.OpenAsync(
            customerId, uploaded.Attachment!.Id, CancellationToken.None);
        CustomerAttachmentContent? foreign = await service.OpenAsync(
            otherCustomerId, uploaded.Attachment.Id, CancellationToken.None);

        Assert.NotNull(owned);
        Assert.Equal("readable.txt", owned!.Value.OriginalFileName);
        await using (Stream content = owned.Value.Content)
        {
            using StreamReader reader = new(content);
            Assert.Equal("attachment-bytes", await reader.ReadToEndAsync());
        }

        Assert.Null(foreign);
    }

    [Fact]
    public async Task Open_RemovedAttachment_IsNotReadable()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerAttachmentService service = CreateService(context, new RecordingAuditRecorder(), out _);
        CustomerAttachmentResult uploaded = await service.UploadAsync(
            customerId, CreateUpload("gone.pdf"), null, CancellationToken.None);
        await service.RemoveAsync(customerId, uploaded.Attachment!.Id, CancellationToken.None);

        CustomerAttachmentContent? content = await service.OpenAsync(
            customerId, uploaded.Attachment.Id, CancellationToken.None);

        Assert.Null(content);
    }

    private static FileUpload CreateUpload(string fileName, string contentType = "application/pdf")
    {
        byte[] bytes = Encoding.UTF8.GetBytes("attachment-bytes");
        return new FileUpload(new MemoryStream(bytes), fileName, contentType, bytes.Length, UploaderHandle);
    }

    private static async Task<Guid> CreateCustomerAsync(CustomerManagementDbContext context)
    {
        CustomerService customerService = new(
            context,
            new StaticCurrentUserAccessor(UploaderHandle),
            new RecordingAuditRecorder(),
            new AlwaysActiveDepartmentLookup(),
            new AlwaysActiveBranchLookup());
        CustomerMutationResult result = await customerService.CreateAsync(
            new CreateCustomerRequest($"First{Guid.NewGuid():N}", "Last", null, null, null), CancellationToken.None);
        return result.Customer!.Id;
    }

    private static CustomerAttachmentService CreateService(
        CustomerManagementDbContext context, IAuditRecorder auditRecorder, out InMemoryFileStorage storage)
    {
        storage = new InMemoryFileStorage();
        return new CustomerAttachmentService(
            context, storage, new StaticCurrentUserAccessor(UploaderHandle), auditRecorder);
    }

    private sealed class StaticCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class AlwaysActiveDepartmentLookup : IDepartmentActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class AlwaysActiveBranchLookup : IBranchActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken) => Task.FromResult(true);
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

    private sealed class RejectingFileStorage : IFileStorage
    {
        public Task<FileReference> UploadAsync(FileUpload upload, CancellationToken cancellationToken = default) =>
            throw new FileValidationException("File content type is not allowed.");

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
