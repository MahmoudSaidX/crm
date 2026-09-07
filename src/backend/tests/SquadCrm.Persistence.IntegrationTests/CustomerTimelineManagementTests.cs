using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerTimelineManagementTests
{
    private static readonly Guid AuthorUserId = Guid.NewGuid();

    public CustomerTimelineManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task GetAsync_UnknownCustomer_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerTimelineService service = new(context);

        CustomerTimelineResult result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(CustomerTimelineFailure.CustomerNotFound, result.Failure);
    }

    [Fact]
    public async Task GetAsync_NewCustomer_ReturnsSingleCreatedEntry()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerTimelineService service = new(context);

        CustomerTimelineResult result = await service.GetAsync(customerId, CancellationToken.None);

        Assert.Equal(CustomerTimelineFailure.None, result.Failure);
        CustomerTimelineEntryResponse entry = Assert.Single(result.Entries!);
        Assert.Equal("CustomerCreated", entry.EventType);
        Assert.Equal(CustomerTimelineVisibility.Customer, entry.Visibility);
    }

    [Fact]
    public async Task GetAsync_WithNotesContactsAndAttachments_ReturnsAllInChronologicalOrder()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);

        CustomerNoteService noteService = new(
            context, new StaticCurrentUserAccessor(AuthorUserId.ToString()), new RecordingAuditRecorder());
        await noteService.AddAsync(customerId, new AddCustomerNoteRequest("Customer called."), CancellationToken.None);

        CustomerContactService contactService = new(
            context, new StaticCurrentUserAccessor(AuthorUserId.ToString()), new RecordingAuditRecorder());
        await contactService.AddAsync(
            customerId,
            new AddCustomerContactRequest(CustomerContactType.Email, "customer@example.test", null, true),
            CancellationToken.None);

        context.CustomerAttachments.Add(new CustomerAttachment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            StorageKey = "key",
            OriginalFileName = "contract.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            UploadedBy = "agent@example.test",
            UploadedAtUtc = DateTimeOffset.UtcNow,
        });
        context.CustomerAttachments.Add(new CustomerAttachment
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            StorageKey = "removed-key",
            OriginalFileName = "removed.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            UploadedBy = "agent@example.test",
            UploadedAtUtc = DateTimeOffset.UtcNow,
            RemovedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        CustomerTimelineService timelineService = new(context);
        CustomerTimelineResult result = await timelineService.GetAsync(customerId, CancellationToken.None);

        Assert.Equal(CustomerTimelineFailure.None, result.Failure);
        List<CustomerTimelineEntryResponse> entries = result.Entries!;
        Assert.Equal(4, entries.Count);
        Assert.DoesNotContain(entries, entry => entry.RelatedEntityType == "CustomerAttachment"
            && entry.Summary == "removed.pdf");
        Assert.Contains(entries, entry => entry.EventType == "NoteAdded"
            && entry.Visibility == CustomerTimelineVisibility.Internal);
        Assert.Contains(entries, entry => entry.EventType == "ContactAdded"
            && entry.Visibility == CustomerTimelineVisibility.Customer);
        Assert.Contains(entries, entry => entry.EventType == "AttachmentAdded"
            && entry.Summary == "contract.pdf");
        Assert.Equal(entries.OrderBy(entry => entry.OccurredAtUtc).ToList(), entries);
    }

    private static async Task<Guid> CreateCustomerAsync(CustomerManagementDbContext context)
    {
        CustomerService customerService = new(
            context,
            new StaticCurrentUserAccessor(AuthorUserId.ToString()),
            new RecordingAuditRecorder(),
            new AlwaysActiveDepartmentLookup(),
            new AlwaysActiveBranchLookup());
        CustomerMutationResult result = await customerService.CreateAsync(
            new CreateCustomerRequest($"First{Guid.NewGuid():N}", "Last", null, null, null), CancellationToken.None);
        return result.Customer!.Id;
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
}
