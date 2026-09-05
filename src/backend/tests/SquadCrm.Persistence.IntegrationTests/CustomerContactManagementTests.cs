using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerContactManagementTests
{
    public CustomerContactManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Add_Email_NormalizesAndRecordsAuditEntry()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerContactService service = CreateService(context, auditRecorder);

        CustomerContactMutationResult result = await service.AddAsync(
            customerId,
            new AddCustomerContactRequest(CustomerContactType.Email, "  Agent@Example.TEST  ", "Work", IsPrimary: false),
            CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.None, result.Failure);
        Assert.Equal("agent@example.test", result.Contact!.NormalizedValue);
        Assert.True(result.Contact.IsActive);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "contact_added" && request.EntityId == result.Contact.Id.ToString());
    }

    [Fact]
    public async Task Add_Phone_NormalizesToDigitsOnly()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());

        CustomerContactMutationResult result = await service.AddAsync(
            customerId,
            new AddCustomerContactRequest(CustomerContactType.Phone, "+966 (55) 123-4567", null, IsPrimary: false),
            CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.None, result.Failure);
        Assert.Equal("966551234567", result.Contact!.NormalizedValue);
    }

    [Fact]
    public async Task Add_InvalidEmail_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());

        CustomerContactMutationResult result = await service.AddAsync(
            customerId,
            new AddCustomerContactRequest(CustomerContactType.Email, "not-an-email", null, IsPrimary: false),
            CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.InvalidValue, result.Failure);
    }

    [Fact]
    public async Task Add_UnknownCustomer_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());

        CustomerContactMutationResult result = await service.AddAsync(
            Guid.NewGuid(),
            new AddCustomerContactRequest(CustomerContactType.Email, "agent@example.test", null, IsPrimary: false),
            CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.CustomerNotFound, result.Failure);
    }

    [Fact]
    public async Task Add_SecondPrimaryOfSameType_DemotesPreviousPrimary()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());
        CustomerContactMutationResult first = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "first@example.test", null, IsPrimary: true),
            CancellationToken.None);

        CustomerContactMutationResult second = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "second@example.test", null, IsPrimary: true),
            CancellationToken.None);

        List<CustomerContact> contacts = await service.ListAsync(customerId, CancellationToken.None);
        Assert.True(second.Contact!.IsPrimary);
        Assert.False(contacts.Single(c => c.Id == first.Contact!.Id).IsPrimary);
    }

    [Fact]
    public async Task Update_ChangingValueAndPrimary_PersistsAndSwapsPrimary()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerContactService service = CreateService(context, auditRecorder);
        CustomerContactMutationResult primary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Phone, "0501234567", null, IsPrimary: true),
            CancellationToken.None);
        CustomerContactMutationResult secondary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Phone, "0509876543", null, IsPrimary: false),
            CancellationToken.None);

        CustomerContactMutationResult updated = await service.UpdateAsync(
            customerId, secondary.Contact!.Id,
            new UpdateCustomerContactRequest("0509876500", "Mobile", IsPrimary: true),
            CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.None, updated.Failure);
        Assert.True(updated.Contact!.IsPrimary);
        Assert.Equal("0509876500", updated.Contact.NormalizedValue);
        List<CustomerContact> contacts = await service.ListAsync(customerId, CancellationToken.None);
        Assert.False(contacts.Single(c => c.Id == primary.Contact!.Id).IsPrimary);
        Assert.Single(auditRecorder.Requests, request => request.Action == "contact_updated");
    }

    [Fact]
    public async Task Deactivate_NonPrimaryContact_Succeeds()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());
        CustomerContactMutationResult added = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "agent@example.test", null, IsPrimary: false),
            CancellationToken.None);

        CustomerContactMutationResult result = await service.DeactivateAsync(
            customerId, added.Contact!.Id, new DeactivateCustomerContactRequest(null), CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.None, result.Failure);
        Assert.False(result.Contact!.IsActive);
    }

    [Fact]
    public async Task Deactivate_PrimaryWithOtherActiveContacts_RequiresNewPrimary()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());
        CustomerContactMutationResult primary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "primary@example.test", null, IsPrimary: true),
            CancellationToken.None);
        await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "secondary@example.test", null, IsPrimary: false),
            CancellationToken.None);

        CustomerContactMutationResult result = await service.DeactivateAsync(
            customerId, primary.Contact!.Id, new DeactivateCustomerContactRequest(null), CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.RequiresNewPrimary, result.Failure);
    }

    [Fact]
    public async Task Deactivate_PrimaryWithInvalidNewPrimary_IsRejected()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());
        CustomerContactMutationResult primary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "primary@example.test", null, IsPrimary: true),
            CancellationToken.None);
        await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "secondary@example.test", null, IsPrimary: false),
            CancellationToken.None);

        CustomerContactMutationResult result = await service.DeactivateAsync(
            customerId, primary.Contact!.Id, new DeactivateCustomerContactRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.InvalidNewPrimary, result.Failure);
    }

    [Fact]
    public async Task Deactivate_PrimaryWithValidNewPrimary_PromotesReplacement()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        RecordingAuditRecorder auditRecorder = new();
        CustomerContactService service = CreateService(context, auditRecorder);
        CustomerContactMutationResult primary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "primary@example.test", null, IsPrimary: true),
            CancellationToken.None);
        CustomerContactMutationResult secondary = await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "secondary@example.test", null, IsPrimary: false),
            CancellationToken.None);

        CustomerContactMutationResult result = await service.DeactivateAsync(
            customerId, primary.Contact!.Id, new DeactivateCustomerContactRequest(secondary.Contact!.Id), CancellationToken.None);

        Assert.Equal(CustomerContactMutationFailure.None, result.Failure);
        List<CustomerContact> contacts = await service.ListAsync(customerId, CancellationToken.None);
        Assert.True(contacts.Single(c => c.Id == secondary.Contact.Id).IsPrimary);
        Assert.Single(auditRecorder.Requests, request => request.Action == "contact_deactivated");
    }

    [Fact]
    public async Task List_ReturnsContactsOrderedByTypeThenCreated()
    {
        await using CustomerManagementDbContext context = PostgresTestDatabase.CreateCustomerManagementContext();
        Guid customerId = await CreateCustomerAsync(context);
        CustomerContactService service = CreateService(context, new RecordingAuditRecorder());
        await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Phone, "0501111111", null, IsPrimary: false),
            CancellationToken.None);
        await service.AddAsync(
            customerId, new AddCustomerContactRequest(CustomerContactType.Email, "one@example.test", null, IsPrimary: false),
            CancellationToken.None);

        List<CustomerContact> contacts = await service.ListAsync(customerId, CancellationToken.None);

        Assert.Equal(2, contacts.Count);
        Assert.Equal(CustomerContactType.Email, contacts[0].Type);
        Assert.Equal(CustomerContactType.Phone, contacts[1].Type);
    }

    private static async Task<Guid> CreateCustomerAsync(CustomerManagementDbContext context)
    {
        CustomerService customerService = new(
            context,
            new StaticCurrentUserAccessor("agent@example.test"),
            new RecordingAuditRecorder(),
            new AlwaysActiveDepartmentLookup(),
            new AlwaysActiveBranchLookup());
        CustomerMutationResult result = await customerService.CreateAsync(
            new CreateCustomerRequest($"First{Guid.NewGuid():N}", "Last", null, null, null), CancellationToken.None);
        return result.Customer!.Id;
    }

    private static CustomerContactService CreateService(CustomerManagementDbContext context, IAuditRecorder auditRecorder) =>
        new(context, new StaticCurrentUserAccessor("agent@example.test"), auditRecorder);

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
