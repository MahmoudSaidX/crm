using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
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

    [Fact]
    public async Task GetDetail_ReturnsTicketWithResolvedCategoryAndPriorityNames()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        TicketMutationResult created = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        TicketDetailResponse? detail = await service.GetDetailAsync(created.Ticket!.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(created.Ticket.TicketNumber, detail!.TicketNumber);
        Assert.Equal(created.Ticket.Subject, detail.Subject);
        Assert.Equal(created.Ticket.Description, detail.Description);
        Assert.Equal(TicketStatus.Open, detail.Status);
        Assert.Equal("Category", detail.CategoryEnglishName);
        Assert.Equal("فئة", detail.CategoryArabicName);
        Assert.True(detail.CategoryIsActive);
        Assert.Equal("Priority", detail.PriorityEnglishName);
        Assert.True(detail.PriorityIsActive);
        Assert.Equal(1, detail.PriorityRank);
        Assert.Null(detail.UpdatedAtUtc);
        Assert.Equal(1, detail.Version);
    }

    /// <summary>
    /// BR: historical labels must not disappear merely because the reference
    /// data was deactivated after the ticket was created.
    /// </summary>
    [Fact]
    public async Task GetDetail_StillReturnsNames_WhenCategoryAndPriorityWereDeactivated()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        TicketMutationResult created = await service.CreateAsync(ValidRequest(categoryId, priorityId), CancellationToken.None);

        TicketCategory category = await context.TicketCategories.SingleAsync(item => item.Id == categoryId);
        TicketPriority priority = await context.TicketPriorities.SingleAsync(item => item.Id == priorityId);
        category.IsActive = false;
        priority.IsActive = false;
        await context.SaveChangesAsync();

        TicketDetailResponse? detail = await service.GetDetailAsync(created.Ticket!.Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Category", detail!.CategoryEnglishName);
        Assert.Equal("Priority", detail.PriorityEnglishName);
        Assert.False(detail.CategoryIsActive);
        Assert.False(detail.PriorityIsActive);
    }

    [Fact]
    public async Task GetDetail_UnknownId_ReturnsNull()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        Assert.Null(await service.GetDetailAsync(Guid.NewGuid(), CancellationToken.None));
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

    [Fact]
    public async Task Assign_UnassignedTicket_SetsOwner_WritesHistory_BumpsVersion_AndWritesOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid agentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(agentId, true));

        TicketMutationResult result = await service.AssignAsync(
            ticketId, new AssignTicketRequest(agentId, null, 1), TicketAssignmentSource.Manual, CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(agentId, result.Ticket!.AssignedAgentId);
        Assert.Equal(2, result.Ticket.Version);
        Assert.NotNull(result.Ticket.UpdatedAtUtc);

        TicketAssignmentHistory history = await context.TicketAssignmentHistory
            .AsNoTracking()
            .SingleAsync(entry => entry.TicketId == ticketId);
        Assert.Null(history.PreviousAgentId);
        Assert.Equal(agentId, history.NewAgentId);
        Assert.Equal(TicketAssignmentSource.Manual, history.Source);
        Assert.Equal("lead@example.test", history.ChangedBy);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "ticket-management.ticket-assigned.v1"
                && message.Payload.Contains(ticketId.ToString()));
        Assert.Contains(agentId.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "assigned" && request.EntityId == ticketId.ToString());
    }

    [Fact]
    public async Task Reassign_WithoutReason_Fails_AndLeavesOwnerUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid firstAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(firstAgentId, true));
        await service.AssignAsync(
            ticketId, new AssignTicketRequest(firstAgentId, null, 1), TicketAssignmentSource.Manual, CancellationToken.None);

        TicketMutationResult result = await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(Guid.NewGuid(), "   ", 2),
            TicketAssignmentSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.ReasonRequired, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(firstAgentId, stored.AssignedAgentId);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task Reassign_WithReason_RecordsPreviousOwner()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid firstAgentId = Guid.NewGuid();
        Guid secondAgentId = Guid.NewGuid();
        TicketService firstService = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(firstAgentId, true));
        await firstService.AssignAsync(
            ticketId, new AssignTicketRequest(firstAgentId, null, 1), TicketAssignmentSource.Manual, CancellationToken.None);
        TicketService secondService = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(secondAgentId, true));

        TicketMutationResult result = await secondService.AssignAsync(
            ticketId,
            new AssignTicketRequest(secondAgentId, "Original agent is on leave.", 2),
            TicketAssignmentSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(secondAgentId, result.Ticket!.AssignedAgentId);
        Assert.Equal(3, result.Ticket.Version);

        TicketAssignmentHistory latest = await context.TicketAssignmentHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderByDescending(entry => entry.ChangedAtUtc)
            .FirstAsync();
        Assert.Equal(firstAgentId, latest.PreviousAgentId);
        Assert.Equal(secondAgentId, latest.NewAgentId);
        Assert.Equal("Original agent is on leave.", latest.Reason);
    }

    [Fact]
    public async Task Assign_InactiveAgent_Fails_AndLeavesTicketUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid agentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(agentId, false));

        TicketMutationResult result = await service.AssignAsync(
            ticketId, new AssignTicketRequest(agentId, null, 1), TicketAssignmentSource.Manual, CancellationToken.None);

        Assert.Equal(TicketMutationFailure.IneligibleAgent, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Null(stored.AssignedAgentId);
        Assert.Equal(1, stored.Version);
        Assert.Empty(context.TicketAssignmentHistory.Where(entry => entry.TicketId == ticketId));
    }

    [Fact]
    public async Task Assign_UnknownAgent_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test", targetAgent: null);

        TicketMutationResult result = await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(Guid.NewGuid(), null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.IneligibleAgent, result.Failure);
    }

    [Fact]
    public async Task Assign_StaleVersion_Fails_AndLeavesOwnerUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid firstAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(firstAgentId, true));
        await service.AssignAsync(
            ticketId, new AssignTicketRequest(firstAgentId, null, 1), TicketAssignmentSource.Manual, CancellationToken.None);

        // Version 1 is what a caller that read the ticket BEFORE the first
        // assignment would send.
        TicketMutationResult result = await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(Guid.NewGuid(), "Taking over.", 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.StaleVersion, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(firstAgentId, stored.AssignedAgentId);
    }

    [Fact]
    public async Task Assign_UnknownTicket_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(Guid.NewGuid(), true));

        TicketMutationResult result = await service.AssignAsync(
            Guid.NewGuid(),
            new AssignTicketRequest(Guid.NewGuid(), null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.TicketNotFound, result.Failure);
    }

    /// <summary>Creates one persisted open, unassigned ticket and returns its id.</summary>
    private static async Task<Guid> SeedTicketAsync(
        TicketManagementDbContext context, RecordingAuditRecorder auditRecorder)
    {
        (Guid categoryId, Guid priorityId) = await SeedCategoryAndPriorityAsync(context);
        TicketService service = CreateService(context, auditRecorder, "agent@example.test");
        TicketMutationResult created = await service.CreateAsync(
            ValidRequest(categoryId, priorityId), CancellationToken.None);
        auditRecorder.Requests.Clear();
        return created.Ticket!.Id;
    }

    private static TicketService CreateService(
        TicketManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle,
        bool customerExists = true,
        bool departmentActive = true,
        bool branchActive = true,
        StaffSubjectReference? targetAgent = null) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubCustomerExistsLookup(customerExists),
            new StubDepartmentActiveLookup(departmentActive),
            new StubBranchActiveLookup(branchActive),
            new StubStaffSubjectReferenceReader(targetAgent));

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

    /// <summary>
    /// Returns the configured subject for ANY id — the assignment tests care
    /// about the active/unknown distinction, not about id matching.
    /// </summary>
    private sealed class StubStaffSubjectReferenceReader(StaffSubjectReference? subject)
        : IStaffSubjectReferenceReader
    {
        public Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(subject);

        public Task<StaffSubjectReference?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(subject);
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
