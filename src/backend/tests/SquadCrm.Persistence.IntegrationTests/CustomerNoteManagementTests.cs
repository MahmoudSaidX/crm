using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerNoteManagementTests
{
    private static readonly Guid AuthorUserId = Guid.NewGuid();

    public CustomerNoteManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Add_ValidNote_PersistsAndRecordsAuditEntry()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerNoteService service = CreateService(context, auditRecorder);

        CustomerNoteMutationResult result = await service.AddAsync(
            customerId, new AddCustomerNoteRequest("  Customer called about billing.  "), CancellationToken.None);

        Assert.Equal(CustomerNoteMutationFailure.None, result.Failure);
        Assert.Equal("Customer called about billing.", result.Note!.Body);
        Assert.Equal(AuthorUserId, result.Note.AuthorUserId);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "note_added" && request.EntityId == result.Note.Id.ToString());
    }

    [Fact]
    public async Task Add_UnknownCustomer_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerNoteService service = CreateService(context, new RecordingAuditRecorder());

        CustomerNoteMutationResult result = await service.AddAsync(
            Guid.NewGuid(), new AddCustomerNoteRequest("Some note"), CancellationToken.None);

        Assert.Equal(CustomerNoteMutationFailure.CustomerNotFound, result.Failure);
    }

    [Fact]
    public async Task List_ReturnsNotesInChronologicalOrder()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerNoteService service = CreateService(context, new RecordingAuditRecorder());
        CustomerNoteMutationResult first = await service.AddAsync(
            customerId, new AddCustomerNoteRequest("First note"), CancellationToken.None);
        CustomerNoteMutationResult second = await service.AddAsync(
            customerId, new AddCustomerNoteRequest("Second note"), CancellationToken.None);

        List<CustomerNote> notes = await service.ListAsync(customerId, CancellationToken.None);

        Assert.Equal(2, notes.Count);
        Assert.Equal(first.Note!.Id, notes[0].Id);
        Assert.Equal(second.Note!.Id, notes[1].Id);
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

    private static CustomerNoteService CreateService(CustomerManagementDbContext context, IAuditRecorder auditRecorder) =>
        new(context, new StaticCurrentUserAccessor(AuthorUserId.ToString()), auditRecorder);

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
