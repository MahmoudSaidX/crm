using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerManagementTests
{
    public CustomerManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_GeneratesCustomerNumber_AndRecordsOneCreatedAuditEntry()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        CustomerService service = CreateService(context, auditRecorder, "agent@example.test");

        CustomerMutationResult result = await service.CreateAsync(
            new CreateCustomerRequest("Sara", "Ahmed", CustomerPreferredLanguage.Arabic, null, null),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.None, result.Failure);
        Assert.NotNull(result.Customer);
        Assert.False(string.IsNullOrWhiteSpace(result.Customer!.CustomerNumber));
        Assert.Equal(CustomerStatus.Active, result.Customer.Status);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.Customer.Id.ToString() && request.ActorHandle == "agent@example.test");
    }

    [Fact]
    public async Task DuplicateCustomer_SameNameAndScope_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        CreateCustomerRequest request = new("Omar", "Khaled", null, null, null);

        CustomerMutationResult first = await service.CreateAsync(request, CancellationToken.None);
        Assert.Equal(CustomerMutationFailure.None, first.Failure);

        CustomerMutationResult second = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.DuplicateCustomer, second.Failure);
    }

    [Fact]
    public async Task InactiveDepartment_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", departmentActive: false);

        CustomerMutationResult result = await service.CreateAsync(
            new CreateCustomerRequest("Layla", "Nasser", null, Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.InactiveDepartment, result.Failure);
    }

    [Fact]
    public async Task InactiveBranch_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", branchActive: false);

        CustomerMutationResult result = await service.CreateAsync(
            new CreateCustomerRequest("Nour", "Saleh", null, null, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.InactiveBranch, result.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using CustomerManagementDbContext firstContext = PostgresTestDatabase.CreateCustomerManagementContext();
        await using CustomerManagementDbContext secondContext = PostgresTestDatabase.CreateCustomerManagementContext();
        CreateCustomerRequest request = new("Yousef", "Hassan", null, null, null);

        Task<CustomerMutationResult> first = CreateService(firstContext, new RecordingAuditRecorder(), "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<CustomerMutationResult> second = CreateService(secondContext, new RecordingAuditRecorder(), "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        CustomerMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == CustomerMutationFailure.None);
        Assert.Single(results, result => result.Failure == CustomerMutationFailure.DuplicateCustomer);
    }

    private static CustomerService CreateService(
        CustomerManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle,
        bool departmentActive = true,
        bool branchActive = true) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubDepartmentActiveLookup(departmentActive),
            new StubBranchActiveLookup(branchActive));

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class StubDepartmentActiveLookup(bool isActive) : IDepartmentActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(isActive);
    }

    private sealed class StubBranchActiveLookup(bool isActive) : IBranchActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(isActive);
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
