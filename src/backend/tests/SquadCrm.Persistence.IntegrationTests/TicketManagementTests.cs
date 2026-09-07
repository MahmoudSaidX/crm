using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.TicketManagement;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class TicketManagementTests
{
    public TicketManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_GeneratesTicketNumber_RecordsAudit_AndWritesOneOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, auditRecorder, "agent@example.test");
        CreateTicketRequest request = ValidRequest(categoryId, priorityId);

        TicketMutationResult result = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.NotNull(result.Ticket);
        Assert.False(string.IsNullOrWhiteSpace(result.Ticket!.TicketNumber));
        Assert.Equal(TicketStatus.Open, result.Ticket.Status);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.Ticket.Id.ToString() && request.ActorHandle == "agent@example.test");

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Payload.Contains(result.Ticket.Id.ToString()));
        Assert.Equal("ticket-management.ticket-created.v1", outboxMessage.Type);
    }

    [Fact]
    public async Task Create_UnknownCustomer_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", customerExists: false);

        TicketMutationResult result = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidCustomer, result.Failure);
    }

    [Fact]
    public async Task Create_InactiveCategory_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context, categoryActive: false);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketMutationResult result = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InactiveCategory, result.Failure);
    }

    [Fact]
    public async Task Create_InactivePriority_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context, priorityActive: false);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketMutationResult result = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InactivePriority, result.Failure);
    }

    [Fact]
    public async Task Create_InactiveDepartment_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", departmentActive: false);

        TicketMutationResult result = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InactiveDepartment, result.Failure);
    }

    [Fact]
    public async Task Create_InactiveBranch_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", branchActive: false);

        TicketMutationResult result = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InactiveBranch, result.Failure);
    }

    [Fact]
    public async Task TwoCreatesForSameCustomer_BothSucceed_WithDistinctTicketNumbers()
    {
        // The generated-number race path (mirrored from CustomerService) is
        // not realistically triggerable here: the ticket number is random per
        // call, unlike a code-based dedup value, so there is no false-
        // duplicate rejection to prove instead.
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        Guid customerId = Guid.NewGuid();
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketMutationResult first = await service.CreateAsync(ValidRequest(categoryId, priorityId, customerId), CancellationToken.None);
        TicketMutationResult second = await service.CreateAsync(ValidRequest(categoryId, priorityId, customerId), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, first.Failure);
        Assert.Equal(TicketMutationFailure.None, second.Failure);
        Assert.NotEqual(first.Ticket!.TicketNumber, second.Ticket!.TicketNumber);
    }

    [Fact]
    public async Task List_FiltersByStatusCategoryPriorityAssigneeDepartmentBranchChannel()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        (Guid otherCategoryId, Guid otherPriorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid assigneeId = Guid.NewGuid();
        Guid departmentId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();

        CreateTicketRequest matching = new(
            Guid.NewGuid(), "Cannot log in", "Description", categoryId, null, priorityId,
            departmentId, branchId, TicketChannel.Agent, assigneeId);
        CreateTicketRequest nonMatching = ValidRequest(otherCategoryId, otherPriorityId);
        await service.CreateAsync(matching, CancellationToken.None);
        await service.CreateAsync(nonMatching, CancellationToken.None);

        PagedResult<Ticket> result = await service.ListAsync(
            new TicketListQuery(
                Statuses: [TicketStatus.Open],
                CategoryIds: [categoryId],
                PriorityIds: [priorityId],
                AssigneeIds: [assigneeId],
                DepartmentIds: [departmentId],
                BranchIds: [branchId],
                Channels: [TicketChannel.Agent]),
            new PaginationRequest(1, 20),
            CancellationToken.None);

        Ticket ticket = Assert.Single(result.Items);
        Assert.Equal(categoryId, ticket.CategoryId);
        Assert.Equal(assigneeId, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task List_SearchMatchesTicketNumberAndSubject()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        TicketMutationResult created = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        PagedResult<Ticket> byNumber = await service.ListAsync(
            new TicketListQuery(Search: created.Ticket!.TicketNumber), new PaginationRequest(1, 20), CancellationToken.None);
        PagedResult<Ticket> bySubject = await service.ListAsync(
            new TicketListQuery(Search: "Cannot log in"), new PaginationRequest(1, 20), CancellationToken.None);

        Assert.Contains(byNumber.Items, t => t.Id == created.Ticket.Id);
        Assert.Contains(bySubject.Items, t => t.Id == created.Ticket.Id);
    }

    [Fact]
    public async Task List_DefaultSortByTicketNumberIsDeterministicAndStable()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);
        await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        PagedResult<Ticket> first = await service.ListAsync(new TicketListQuery(), new PaginationRequest(1, 20), CancellationToken.None);
        PagedResult<Ticket> second = await service.ListAsync(new TicketListQuery(), new PaginationRequest(1, 20), CancellationToken.None);

        Assert.Equal(first.Items.Select(t => t.Id), second.Items.Select(t => t.Id));
        Assert.Equal(first.Items.Select(t => t.TicketNumber).OrderBy(n => n), first.Items.Select(t => t.TicketNumber));
    }

    [Fact]
    public async Task List_PaginationBoundaries_RespectPageSizeAndPage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        for (int i = 0; i < 3; i++)
        {
            await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);
        }

        PagedResult<Ticket> page1 = await service.ListAsync(new TicketListQuery(), new PaginationRequest(1, 2), CancellationToken.None);
        PagedResult<Ticket> page2 = await service.ListAsync(new TicketListQuery(), new PaginationRequest(2, 2), CancellationToken.None);

        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount >= 3);
        Assert.DoesNotContain(page1.Items.Select(t => t.Id), id => page2.Items.Any(t => t.Id == id));
    }

    [Fact]
    public async Task List_FiltersNeverExpandBeyondUnfilteredResults()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        PagedResult<Ticket> unfiltered = await service.ListAsync(new TicketListQuery(), new PaginationRequest(1, 100), CancellationToken.None);
        PagedResult<Ticket> filtered = await service.ListAsync(
            new TicketListQuery(CategoryIds: [categoryId]), new PaginationRequest(1, 100), CancellationToken.None);

        HashSet<Guid> unfilteredIds = unfiltered.Items.Select(t => t.Id).ToHashSet();
        Assert.All(filtered.Items, t => Assert.Contains(t.Id, unfilteredIds));
    }

    private static async Task<(Guid CategoryId, Guid PriorityId)> SeedCategoryAndPriorityAsync(
        TicketManagementDbContext context, bool categoryActive = true, bool priorityActive = true)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TicketCategory category = new()
        {
            Id = Guid.NewGuid(),
            Code = $"CAT-{Guid.NewGuid():N}"[..12],
            NormalizedCode = $"CAT-{Guid.NewGuid():N}"[..12],
            ArabicName = "فئة",
            EnglishName = "Category",
            IsActive = categoryActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        TicketPriority priority = new()
        {
            Id = Guid.NewGuid(),
            Code = $"PRI-{Guid.NewGuid():N}"[..12],
            NormalizedCode = $"PRI-{Guid.NewGuid():N}"[..12],
            ArabicName = "أولوية",
            EnglishName = "Priority",
            Rank = 1,
            IsActive = priorityActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        context.TicketCategories.Add(category);
        context.TicketPriorities.Add(priority);
        await context.SaveChangesAsync();
        return (category.Id, priority.Id);
    }

    private static CreateTicketRequest ValidRequest(Guid categoryId, Guid priorityId, Guid? customerId = null) => new(
        customerId ?? Guid.NewGuid(),
        "Cannot log in",
        "The customer cannot log in to the portal.",
        categoryId,
        null,
        priorityId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        TicketChannel.Agent,
        null);

    private static TicketService CreateService(
        TicketManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle,
        bool customerExists = true,
        bool departmentActive = true,
        bool branchActive = true) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubCustomerExistsLookup(customerExists),
            new StubDepartmentActiveLookup(departmentActive),
            new StubBranchActiveLookup(branchActive));

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class StubCustomerExistsLookup(bool exists) : ICustomerExistsLookup
    {
        public Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(exists);
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
