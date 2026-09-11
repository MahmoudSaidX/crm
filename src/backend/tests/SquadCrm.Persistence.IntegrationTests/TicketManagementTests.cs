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
        // Unique subject: the shared "Cannot log in" fixture subject matches
        // every ticket other tests seed, so a single page of results is not
        // guaranteed to contain this one.
        string subject = $"Cannot log in {Guid.NewGuid():N}";
        TicketMutationResult created = await service.CreateAsync(
            ValidRequest(categoryId, priorityId, subject: subject), CancellationToken.None);

        PagedResult<Ticket> byNumber = await service.ListAsync(
            new TicketListQuery(Search: created.Ticket!.TicketNumber), new PaginationRequest(1, 20), CancellationToken.None);
        PagedResult<Ticket> bySubject = await service.ListAsync(
            new TicketListQuery(Search: subject), new PaginationRequest(1, 20), CancellationToken.None);

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

    private static CreateTicketRequest ValidRequest(
        Guid categoryId, Guid priorityId, Guid? customerId = null, string? subject = null) => new(
        customerId ?? Guid.NewGuid(),
        subject ?? "Cannot log in",
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

    [Fact]
    public async Task ChangeStatus_ValidTransition_WritesHistory_BumpsVersion_AndWritesOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");

        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.InProgress, null, 1),
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(TicketStatus.InProgress, result.Ticket!.Status);
        Assert.Equal(2, result.Ticket.Version);
        Assert.NotNull(result.Ticket.UpdatedAtUtc);

        TicketStatusHistory history = await context.TicketStatusHistory
            .AsNoTracking()
            .SingleAsync(entry => entry.TicketId == ticketId);
        Assert.Equal(TicketStatus.Open, history.PreviousStatus);
        Assert.Equal(TicketStatus.InProgress, history.NewStatus);
        Assert.Null(history.Reason);
        Assert.Equal("lead@example.test", history.ChangedBy);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "ticket-management.ticket-status-changed.v1"
                && message.Payload.Contains(ticketId.ToString()));
        Assert.Contains("InProgress", outboxMessage.Payload, StringComparison.Ordinal);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "status-changed" && request.EntityId == ticketId.ToString());
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_Fails_AndLeavesTicketUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");

        // Open -> Closed is not in the matrix: closure follows resolution.
        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.Closed, "Duplicate request.", 1),
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidStatusTransition, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(TicketStatus.Open, stored.Status);
        Assert.Equal(1, stored.Version);
        Assert.Empty(context.TicketStatusHistory.Where(entry => entry.TicketId == ticketId));
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_IsRejectedAsInvalidTransition()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");

        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Open, null, 1), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidStatusTransition, result.Failure);
    }

    [Fact]
    public async Task ChangeStatus_CloseWithoutReason_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Resolved, null, 1), CancellationToken.None);

        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Closed, "   ", 2), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.ReasonRequired, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(TicketStatus.Resolved, stored.Status);
    }

    [Fact]
    public async Task Reopen_WithReason_Succeeds_AndPreservesEarlierHistory()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Resolved, null, 1), CancellationToken.None);
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Closed, "Customer confirmed.", 2), CancellationToken.None);

        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.InProgress, "Customer reported it again.", 3),
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(TicketStatus.InProgress, result.Ticket!.Status);
        Assert.Equal(4, result.Ticket.Version);

        List<TicketStatusHistory> history = await context.TicketStatusHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderBy(entry => entry.ChangedAtUtc)
            .ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Equal(TicketStatus.Resolved, history[0].NewStatus);
        Assert.Equal(TicketStatus.Closed, history[1].NewStatus);
        Assert.Equal(TicketStatus.Closed, history[2].PreviousStatus);
        Assert.Equal("Customer reported it again.", history[2].Reason);
    }

    [Fact]
    public async Task ChangeStatus_StaleVersion_Fails_AndLeavesStatusUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test");
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.InProgress, null, 1), CancellationToken.None);

        // Version 1 is what a caller that read the ticket BEFORE the first
        // transition would send.
        TicketMutationResult result = await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Resolved, null, 1), CancellationToken.None);

        Assert.Equal(TicketMutationFailure.StaleVersion, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(TicketStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownTicket_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketService service = CreateService(context, new RecordingAuditRecorder(), "lead@example.test");

        TicketMutationResult result = await service.ChangeStatusAsync(
            Guid.NewGuid(),
            new ChangeTicketStatusRequest(TicketStatus.InProgress, null, 1),
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.TicketNotFound, result.Failure);
    }

    [Fact]
    public async Task GetDetail_ReportsAllowedStatusTransitionsForCurrentStatus()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);

        TicketDetailResponse? detail = await CreateService(context, auditRecorder, "agent@example.test")
            .GetDetailAsync(ticketId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(
            ["InProgress", "PendingCustomer", "PendingInternal", "Resolved"],
            detail!.AllowedStatusTransitions);
    }

    [Fact]
    public async Task Escalate_Succeeds_SetsLevelAndTarget_WritesHistory_BumpsVersion_AndWritesOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs a senior agent.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(1, result.Ticket!.EscalationLevel);
        Assert.Equal(TicketEscalationTargetType.Agent, result.Ticket.EscalationTargetType);
        Assert.Equal(targetAgentId, result.Ticket.EscalationTargetId);
        Assert.NotNull(result.Ticket.EscalatedAtUtc);
        Assert.Equal(2, result.Ticket.Version);

        // Escalation is not a lifecycle status (BR): the status is untouched.
        Assert.Equal(TicketStatus.Open, result.Ticket.Status);

        TicketEscalationHistory history = await context.TicketEscalationHistory
            .AsNoTracking()
            .SingleAsync(entry => entry.TicketId == ticketId);
        Assert.Equal(0, history.PreviousLevel);
        Assert.Equal(1, history.NewLevel);
        Assert.Equal(TicketEscalationTargetType.Agent, history.TargetType);
        Assert.Equal(targetAgentId, history.TargetId);
        Assert.Equal("Needs a senior agent.", history.Reason);
        Assert.Equal(TicketEscalationSource.Manual, history.Source);
        Assert.Equal("lead@example.test", history.EscalatedBy);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "ticket-management.ticket-escalated.v1"
                && message.Payload.Contains(ticketId.ToString()));
        Assert.Contains("Agent", outboxMessage.Payload, StringComparison.Ordinal);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "escalated" && request.EntityId == ticketId.ToString());
    }

    [Fact]
    public async Task Escalate_Twice_ReachesLevelTwo_AndPreservesEarlierHistory()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        Guid departmentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "First escalation.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Department, departmentId, "Still unresolved.", 2),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.None, result.Failure);
        Assert.Equal(2, result.Ticket!.EscalationLevel);
        Assert.Equal(TicketEscalationTargetType.Department, result.Ticket.EscalationTargetType);
        Assert.Equal(departmentId, result.Ticket.EscalationTargetId);

        List<TicketEscalationHistory> history = await context.TicketEscalationHistory
            .AsNoTracking()
            .Where(entry => entry.TicketId == ticketId)
            .OrderBy(entry => entry.EscalatedAtUtc)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(1, history[0].NewLevel);
        Assert.Equal("First escalation.", history[0].Reason);
        Assert.Equal(1, history[1].PreviousLevel);
        Assert.Equal(2, history[1].NewLevel);
    }

    [Fact]
    public async Task Escalate_BlankReason_Fails_AndLeavesTicketUnescalated()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "   ", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.ReasonRequired, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(0, stored.EscalationLevel);
        Assert.Equal(1, stored.Version);
        Assert.Empty(context.TicketEscalationHistory.Where(entry => entry.TicketId == ticketId));
    }

    [Fact]
    public async Task Escalate_InactiveAgentTarget_Fails_AndLeavesTicketUnescalated()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, false));

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs attention.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidEscalationTarget, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(0, stored.EscalationLevel);
        Assert.Null(stored.EscalationTargetId);
    }

    [Fact]
    public async Task Escalate_UnknownAgentTarget_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketService service = CreateService(context, auditRecorder, "lead@example.test", targetAgent: null);

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, Guid.NewGuid(), "Needs attention.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidEscalationTarget, result.Failure);
    }

    [Fact]
    public async Task Escalate_InactiveDepartmentTarget_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);

        // The ticket was seeded through a service whose department lookup says
        // active; this second service reports the ESCALATION target department
        // as inactive.
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", departmentActive: false);

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Department, Guid.NewGuid(), "Needs the team.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.InvalidEscalationTarget, result.Failure);
    }

    [Fact]
    public async Task Escalate_ClosedTicket_IsRejectedAsNotEscalatable()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Resolved, null, 1), CancellationToken.None);
        await service.ChangeStatusAsync(
            ticketId, new ChangeTicketStatusRequest(TicketStatus.Closed, "Customer confirmed.", 2), CancellationToken.None);

        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Too late.", 3),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.TicketNotEscalatable, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(0, stored.EscalationLevel);
    }

    [Fact]
    public async Task Escalate_StaleVersion_Fails_AndLeavesLevelUnchanged()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "First escalation.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        // Version 1 is what a caller that read the ticket BEFORE the first
        // escalation would send: accepting it would bump the level twice for
        // one decision.
        TicketMutationResult result = await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Same decision again.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.StaleVersion, result.Failure);
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(ticket => ticket.Id == ticketId);
        Assert.Equal(1, stored.EscalationLevel);
        Assert.Single(context.TicketEscalationHistory.Where(entry => entry.TicketId == ticketId));
    }

    [Fact]
    public async Task Escalate_UnknownTicket_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketService service = CreateService(
            context,
            new RecordingAuditRecorder(),
            "lead@example.test",
            targetAgent: new StaffSubjectReference(Guid.NewGuid(), true));

        TicketMutationResult result = await service.EscalateAsync(
            Guid.NewGuid(),
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, Guid.NewGuid(), "Needs attention.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        Assert.Equal(TicketMutationFailure.TicketNotFound, result.Failure);
    }

    [Fact]
    public async Task GetDetail_ReportsEscalationState()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs a senior agent.", 1),
            TicketEscalationSource.Manual,
            CancellationToken.None);

        TicketDetailResponse? detail = await service.GetDetailAsync(ticketId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(1, detail!.EscalationLevel);
        Assert.Equal(TicketEscalationTargetType.Agent, detail.EscalationTargetType);
        Assert.Equal(targetAgentId, detail.EscalationTargetId);
        Assert.NotNull(detail.EscalatedAtUtc);
    }


    [Fact]
    public async Task Timeline_NewTicket_ContainsOnlyTheCreationEntry()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        Assert.Equal(TicketTimelineFailure.None, result.Failure);
        TicketTimelineEntryResponse entry = Assert.Single(result.Page!.Items);
        Assert.Equal("TicketCreated", entry.EventType);
        Assert.Equal(1, entry.Sequence);
        Assert.Equal(TicketTimelineActorType.User, entry.ActorType);

        // CRM-133 persists no creator column, so the entry reports "not
        // attributable from this module" rather than inventing an actor.
        Assert.Null(entry.ActorId);
        Assert.Equal(TicketTimelineVisibility.Customer, entry.Visibility);
    }

    [Fact]
    public async Task Timeline_IncludesAssignmentStatusAndEscalationEntries_InChronologicalOrder()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(targetAgentId, null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);
        await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.InProgress, null, 2),
            CancellationToken.None);
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs a senior agent.", 3),
            TicketEscalationSource.Manual,
            CancellationToken.None);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        List<TicketTimelineEntryResponse> entries = [.. result.Page!.Items];
        Assert.Equal(4, entries.Count);
        Assert.Equal(
            ["TicketCreated", "TicketAssigned", "TicketStatusChanged", "TicketEscalated"],
            entries.Select(entry => entry.EventType));
        Assert.Equal([1, 2, 3, 4], entries.Select(entry => entry.Sequence));

        // Chronological, and every entry carries a server timestamp.
        Assert.Equal(
            entries.Select(entry => entry.OccurredAtUtc).Order(),
            entries.Select(entry => entry.OccurredAtUtc));

        Assert.Equal("lead@example.test", entries[1].ActorId);
        Assert.Equal(TicketTimelineVisibility.Internal, entries[1].Visibility);
        Assert.Contains("Open", entries[2].Summary, StringComparison.Ordinal);
        Assert.Contains("InProgress", entries[2].Summary, StringComparison.Ordinal);
        Assert.Equal(TicketTimelineVisibility.Customer, entries[2].Visibility);
        Assert.Equal("Needs a senior agent.", entries[3].Reason);
        Assert.Contains("level 1", entries[3].Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeline_AutomationWrittenHistory_IdentifiesItsSourceDistinctlyFromAHumanActor()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(targetAgentId, null, 1),
            TicketAssignmentSource.Automation,
            CancellationToken.None);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        TicketTimelineEntryResponse assignment =
            result.Page!.Items.Single(entry => entry.EventType == "TicketAssigned");
        Assert.Equal(TicketTimelineActorType.Automation, assignment.ActorType);
    }

    [Fact]
    public async Task Timeline_ReassignmentIsReportedDistinctlyFromFirstAssignment()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid firstAgentId = Guid.NewGuid();
        Guid secondAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(firstAgentId, true));
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(firstAgentId, null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(secondAgentId, "Workload rebalanced.", 2),
            TicketAssignmentSource.Manual,
            CancellationToken.None);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        Assert.Equal(
            ["TicketCreated", "TicketAssigned", "TicketReassigned"],
            result.Page!.Items.Select(entry => entry.EventType));
        Assert.Equal("Workload rebalanced.", result.Page.Items[2].Reason);
    }

    [Fact]
    public async Task Timeline_PagesAreDisjoint_AndTheirUnionIsTheWholeTimeline()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(targetAgentId, null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);
        await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.InProgress, null, 2),
            CancellationToken.None);
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs a senior agent.", 3),
            TicketEscalationSource.Manual,
            CancellationToken.None);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult first = await timelineService.GetAsync(
            ticketId, new PaginationRequest(1, 2), TicketTimelineAudience.Internal, CancellationToken.None);
        TicketTimelineResult second = await timelineService.GetAsync(
            ticketId, new PaginationRequest(2, 2), TicketTimelineAudience.Internal, CancellationToken.None);

        Assert.Equal(4, first.Page!.TotalCount);
        Assert.Equal(4, second.Page!.TotalCount);
        Assert.Equal([1, 2], first.Page.Items.Select(entry => entry.Sequence));
        Assert.Equal([3, 4], second.Page.Items.Select(entry => entry.Sequence));

        // Every event id appears exactly once across the two pages: no overlap,
        // nothing skipped.
        Assert.Equal(
            4,
            first.Page.Items.Concat(second.Page.Items).Select(entry => entry.EventId).Distinct().Count());
    }

    [Fact]
    public async Task Timeline_CustomerAudience_ExcludesInternalEntries_AndStripsReasons()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid ticketId = await SeedTicketAsync(context, auditRecorder);
        Guid targetAgentId = Guid.NewGuid();
        TicketService service = CreateService(
            context, auditRecorder, "lead@example.test", targetAgent: new StaffSubjectReference(targetAgentId, true));
        await service.AssignAsync(
            ticketId,
            new AssignTicketRequest(targetAgentId, null, 1),
            TicketAssignmentSource.Manual,
            CancellationToken.None);
        await service.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(TicketStatus.InProgress, "Internal-only note.", 2),
            CancellationToken.None);
        await service.EscalateAsync(
            ticketId,
            new EscalateTicketRequest(TicketEscalationTargetType.Agent, targetAgentId, "Needs a senior agent.", 3),
            TicketEscalationSource.Manual,
            CancellationToken.None);
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Customer, CancellationToken.None);

        // Assignment and escalation are internal routing: neither appears, and
        // neither occupies a sequence slot in the customer's timeline.
        Assert.Equal(
            ["TicketCreated", "TicketStatusChanged"],
            result.Page!.Items.Select(entry => entry.EventType));
        Assert.Equal(2, result.Page.TotalCount);
        Assert.Equal([1, 2], result.Page.Items.Select(entry => entry.Sequence));
        Assert.All(result.Page.Items, entry => Assert.Null(entry.Reason));
        Assert.All(
            result.Page.Items,
            entry => Assert.Equal(TicketTimelineVisibility.Customer, entry.Visibility));
    }

    [Fact]
    public async Task Timeline_UnknownTicket_Fails()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketTimelineService timelineService = new(context);

        TicketTimelineResult result = await timelineService.GetAsync(
            Guid.NewGuid(), new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        Assert.Equal(TicketTimelineFailure.TicketNotFound, result.Failure);
        Assert.Null(result.Page);
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
