using SquadCrm.BuildingBlocks.Http;
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

    [Fact]
    public async Task List_SearchByCustomerNumberNameCaseInsensitive_ReturnsMatch()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        CustomerMutationResult created = await service.CreateAsync(
            new CreateCustomerRequest("Fatima", "Zahra", null, null, null), CancellationToken.None);

        PagedResult<Customer> byName = await service.ListAsync(
            new CustomerListQuery(Search: "fatima"), new PaginationRequest(), CancellationToken.None);
        PagedResult<Customer> byNumber = await service.ListAsync(
            new CustomerListQuery(Search: created.Customer!.CustomerNumber.ToLowerInvariant()),
            new PaginationRequest(),
            CancellationToken.None);

        Assert.Single(byName.Items, customer => customer.Id == created.Customer.Id);
        Assert.Single(byNumber.Items, customer => customer.Id == created.Customer.Id);
    }

    [Fact]
    public async Task List_FiltersByDepartmentBranchAndStatus()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid departmentId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        CustomerMutationResult scoped = await service.CreateAsync(
            new CreateCustomerRequest("Huda", "Ali", null, departmentId, branchId), CancellationToken.None);
        await service.CreateAsync(new CreateCustomerRequest("Huda", "Ali2", null, null, null), CancellationToken.None);

        PagedResult<Customer> byDepartment = await service.ListAsync(
            new CustomerListQuery(DepartmentIds: [departmentId]), new PaginationRequest(), CancellationToken.None);
        PagedResult<Customer> byBranch = await service.ListAsync(
            new CustomerListQuery(BranchIds: [branchId]), new PaginationRequest(), CancellationToken.None);
        PagedResult<Customer> byStatus = await service.ListAsync(
            new CustomerListQuery(Status: [CustomerStatus.Active]), new PaginationRequest(), CancellationToken.None);

        Assert.Single(byDepartment.Items, customer => customer.Id == scoped.Customer!.Id);
        Assert.Single(byBranch.Items, customer => customer.Id == scoped.Customer!.Id);
        Assert.True(byStatus.TotalCount >= 2);
    }

    [Fact]
    public async Task List_IsDeterministicallySortedAndPaginated_AcrossPages()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        for (int index = 0; index < 5; index++)
        {
            await service.CreateAsync(
                new CreateCustomerRequest($"First{index}", $"Last{index}", null, null, null), CancellationToken.None);
        }

        PagedResult<Customer> pageOne = await service.ListAsync(
            new CustomerListQuery(), new PaginationRequest(Page: 1, PageSize: 2), CancellationToken.None);
        PagedResult<Customer> pageTwo = await service.ListAsync(
            new CustomerListQuery(), new PaginationRequest(Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(2, pageOne.Items.Count);
        Assert.Equal(2, pageTwo.Items.Count);
        Assert.Empty(pageOne.Items.Select(customer => customer.Id).Intersect(pageTwo.Items.Select(customer => customer.Id)));
        List<string> ordered = [.. pageOne.Items.Select(c => c.CustomerNumber), .. pageTwo.Items.Select(c => c.CustomerNumber)];
        Assert.Equal(ordered.OrderBy(number => number, StringComparer.Ordinal), ordered);
    }

    [Fact]
    public async Task Get_ExistingCustomer_ReturnsCustomer()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid departmentId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        CustomerMutationResult created = await service.CreateAsync(
            new CreateCustomerRequest("Mona", "Adel", CustomerPreferredLanguage.English, departmentId, branchId),
            CancellationToken.None);

        Customer? result = await service.GetAsync(created.Customer!.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created.Customer.Id, result!.Id);
        Assert.Equal(created.Customer.CustomerNumber, result.CustomerNumber);
        Assert.Equal("Mona", result.FirstName);
        Assert.Equal("Adel", result.LastName);
        Assert.Equal(CustomerPreferredLanguage.English, result.PreferredLanguage);
        Assert.Equal(departmentId, result.DepartmentId);
        Assert.Equal(branchId, result.BranchId);
        Assert.Equal(CustomerStatus.Active, result.Status);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNull()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        Customer? result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Update_Succeeds_ChangesFieldsAndStatus_AndRecordsOneUpdatedAuditEntry()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        CustomerService service = CreateService(context, auditRecorder, "agent@example.test");
        CustomerMutationResult created = await service.CreateAsync(
            new CreateCustomerRequest("Rania", "Fathy", CustomerPreferredLanguage.Arabic, null, null),
            CancellationToken.None);
        Guid departmentId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();

        CustomerMutationResult result = await service.UpdateAsync(
            created.Customer!.Id,
            new UpdateCustomerRequest(
                "Sara2", "Ahmed2", CustomerPreferredLanguage.English, departmentId, branchId,
                CustomerStatus.Inactive, created.Customer.Version),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.None, result.Failure);
        Assert.Equal("Sara2", result.Customer!.FirstName);
        Assert.Equal("Ahmed2", result.Customer.LastName);
        Assert.Equal(CustomerPreferredLanguage.English, result.Customer.PreferredLanguage);
        Assert.Equal(departmentId, result.Customer.DepartmentId);
        Assert.Equal(branchId, result.Customer.BranchId);
        Assert.Equal(CustomerStatus.Inactive, result.Customer.Status);
        Assert.Equal(created.Customer.CustomerNumber, result.Customer.CustomerNumber);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "updated" && request.EntityId == result.Customer.Id.ToString());
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        CustomerMutationResult result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateCustomerRequest("A", "B", null, null, null, CustomerStatus.Active, 0),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task Update_InactiveDepartment_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        CustomerMutationResult created = await service.CreateAsync(
            new CreateCustomerRequest("Salma", "Naeem", null, null, null), CancellationToken.None);
        CustomerService serviceWithInactiveDepartment = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", departmentActive: false);

        CustomerMutationResult result = await serviceWithInactiveDepartment.UpdateAsync(
            created.Customer!.Id,
            new UpdateCustomerRequest(
                "Salma", "Naeem", null, Guid.NewGuid(), null, CustomerStatus.Active, created.Customer.Version),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.InactiveDepartment, result.Failure);
    }

    [Fact]
    public async Task Update_InactiveBranch_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        CustomerMutationResult created = await service.CreateAsync(
            new CreateCustomerRequest("Tarek", "Younes", null, null, null), CancellationToken.None);
        CustomerService serviceWithInactiveBranch = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", branchActive: false);

        CustomerMutationResult result = await serviceWithInactiveBranch.UpdateAsync(
            created.Customer!.Id,
            new UpdateCustomerRequest(
                "Tarek", "Younes", null, null, Guid.NewGuid(), CustomerStatus.Active, created.Customer.Version),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.InactiveBranch, result.Failure);
    }

    [Fact]
    public async Task Update_StaleVersion_ReturnsConcurrencyConflict()
    {
        await using CustomerManagementDbContext setupContext = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService setupService = CreateService(setupContext, new RecordingAuditRecorder(), "agent@example.test");
        CustomerMutationResult created = await setupService.CreateAsync(
            new CreateCustomerRequest("Dina", "Farouk", null, null, null), CancellationToken.None);

        // Two separate contexts simulate two agents who each read the
        // customer once, then both try to save — the second must be
        // rejected because its read is now stale.
        await using CustomerManagementDbContext firstReaderContext = PostgresTestDatabase.CreateCustomerManagementContext();
        await using CustomerManagementDbContext secondReaderContext = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerService firstReaderService = CreateService(firstReaderContext, new RecordingAuditRecorder(), "agent-one@example.test");
        CustomerService secondReaderService = CreateService(secondReaderContext, new RecordingAuditRecorder(), "agent-two@example.test");

        Customer firstRead = (await firstReaderService.GetAsync(created.Customer!.Id, CancellationToken.None))!;
        Customer secondRead = (await secondReaderService.GetAsync(created.Customer.Id, CancellationToken.None))!;

        CustomerMutationResult firstUpdate = await firstReaderService.UpdateAsync(
            created.Customer.Id,
            new UpdateCustomerRequest(
                "Dina2", "Farouk", null, null, null, CustomerStatus.Active, firstRead.Version),
            CancellationToken.None);
        Assert.Equal(CustomerMutationFailure.None, firstUpdate.Failure);

        CustomerMutationResult staleUpdate = await secondReaderService.UpdateAsync(
            created.Customer.Id,
            new UpdateCustomerRequest(
                "Dina3", "Farouk", null, null, null, CustomerStatus.Active, secondRead.Version),
            CancellationToken.None);

        Assert.Equal(CustomerMutationFailure.ConcurrencyConflict, staleUpdate.Failure);
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
