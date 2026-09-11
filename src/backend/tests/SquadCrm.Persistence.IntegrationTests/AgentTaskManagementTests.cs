using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.AgentTaskManagement;
using SquadCrm.Modules.AgentTaskManagement.Persistence;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.TicketManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class AgentTaskManagementTests
{
    public AgentTaskManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_DefaultsOwnerToCaller_RecordsAudit_AndWritesOneOutboxMessage()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid callerId = Guid.NewGuid();
        AgentTaskService service = CreateService(context, auditRecorder, callerId.ToString());

        AgentTaskMutationResult result = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.NotNull(result.AgentTask);
        Assert.Equal(callerId, result.AgentTask!.OwnerUserId);
        Assert.Equal(AgentTaskStatus.Open, result.AgentTask.Status);
        Assert.Equal(1, result.AgentTask.Version);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.AgentTask.Id.ToString());

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Payload.Contains(result.AgentTask.Id.ToString()));
        Assert.Equal("agent-task-management.agent-task-created.v1", outboxMessage.Type);
    }

    [Fact]
    public async Task Create_ExplicitActiveOwner_IsAccepted()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        Guid callerId = Guid.NewGuid();
        Guid explicitOwnerId = Guid.NewGuid();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), callerId.ToString(),
            targetOwner: new StaffSubjectReference(explicitOwnerId, true));

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { OwnerUserId = explicitOwnerId }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(explicitOwnerId, result.AgentTask!.OwnerUserId);
    }

    [Fact]
    public async Task Create_ExplicitInactiveOwner_IsRejectedAsIneligible()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        Guid callerId = Guid.NewGuid();
        Guid explicitOwnerId = Guid.NewGuid();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), callerId.ToString(),
            targetOwner: new StaffSubjectReference(explicitOwnerId, false));

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { OwnerUserId = explicitOwnerId }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.IneligibleOwner, result.Failure);
    }

    [Fact]
    public async Task Create_ExplicitUnknownOwner_IsRejectedAsIneligible()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString(), targetOwner: null);

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { OwnerUserId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.IneligibleOwner, result.Failure);
    }

    [Fact]
    public async Task Create_UnresolvableCallerHandle_IsRejectedAsOwnerUnresolved()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), handle: null);

        AgentTaskMutationResult result = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.OwnerUnresolved, result.Failure);
    }

    [Fact]
    public async Task Create_UnknownTicketId_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString(), ticketExists: false);

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { TicketId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.InvalidTicket, result.Failure);
    }

    [Fact]
    public async Task Create_UnknownCustomerId_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString(), customerExists: false);

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { CustomerId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.InvalidCustomer, result.Failure);
    }

    /// <summary>
    /// BR: linking a task to a ticket is an existence check only — the task
    /// response never carries anything about the ticket beyond the id the
    /// caller supplied, so no ticket data leaks through the task.
    /// </summary>
    [Fact]
    public async Task Create_WithValidTicketAndCustomer_StoresOnlyTheIds_AndExposesNothingElse()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        Guid ticketId = Guid.NewGuid();
        Guid customerId = Guid.NewGuid();

        AgentTaskMutationResult result = await service.CreateAsync(
            ValidRequest() with { TicketId = ticketId, CustomerId = customerId }, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(ticketId, result.AgentTask!.TicketId);
        Assert.Equal(customerId, result.AgentTask.CustomerId);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        Guid callerId = Guid.NewGuid();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), callerId.ToString());
        AgentTaskMutationResult open = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskMutationResult completed = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.CompleteAsync(completed.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        PagedResult<AgentTask> result = await service.ListAsync(
            new AgentTaskListQuery(Statuses: [AgentTaskStatus.Completed], MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);

        AgentTask task = Assert.Single(result.Items);
        Assert.Equal(completed.AgentTask.Id, task.Id);
        Assert.DoesNotContain(result.Items, t => t.Id == open.AgentTask!.Id);
    }

    [Fact]
    public async Task List_FiltersByDueRange()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentTaskMutationResult early = await service.CreateAsync(
            ValidRequest() with { DueAtUtc = now.AddDays(1) }, CancellationToken.None);
        AgentTaskMutationResult late = await service.CreateAsync(
            ValidRequest() with { DueAtUtc = now.AddDays(10) }, CancellationToken.None);
        AgentTaskMutationResult noDueDate = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        PagedResult<AgentTask> result = await service.ListAsync(
            new AgentTaskListQuery(DueBefore: now.AddDays(5), DueAfter: now, MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);

        AgentTask task = Assert.Single(result.Items);
        Assert.Equal(early.AgentTask!.Id, task.Id);
        Assert.DoesNotContain(result.Items, t => t.Id == late.AgentTask!.Id);
        Assert.DoesNotContain(result.Items, t => t.Id == noDueDate.AgentTask!.Id);
    }

    [Fact]
    public async Task List_SortByDueAtUtc_AscendingAndDescending()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AgentTaskMutationResult first = await service.CreateAsync(
            ValidRequest() with { DueAtUtc = now.AddDays(1) }, CancellationToken.None);
        AgentTaskMutationResult second = await service.CreateAsync(
            ValidRequest() with { DueAtUtc = now.AddDays(2) }, CancellationToken.None);

        PagedResult<AgentTask> ascending = await service.ListAsync(
            new AgentTaskListQuery(SortBy: AgentTaskSortBy.DueAtUtc, SortDirection: SortDirection.Asc, MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);
        PagedResult<AgentTask> descending = await service.ListAsync(
            new AgentTaskListQuery(SortBy: AgentTaskSortBy.DueAtUtc, SortDirection: SortDirection.Desc, MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);

        Assert.Equal([first.AgentTask!.Id, second.AgentTask!.Id], ascending.Items.Select(t => t.Id));
        Assert.Equal([second.AgentTask.Id, first.AgentTask.Id], descending.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_SortByCreatedAtUtc_AscendingAndDescending()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult first = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskMutationResult second = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        PagedResult<AgentTask> ascending = await service.ListAsync(
            new AgentTaskListQuery(SortBy: AgentTaskSortBy.CreatedAtUtc, SortDirection: SortDirection.Asc, MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);
        PagedResult<AgentTask> descending = await service.ListAsync(
            new AgentTaskListQuery(SortBy: AgentTaskSortBy.CreatedAtUtc, SortDirection: SortDirection.Desc, MyTasksOnly: true),
            new PaginationRequest(1, 20),
            CancellationToken.None);

        Assert.Equal([first.AgentTask!.Id, second.AgentTask!.Id], ascending.Items.Select(t => t.Id));
        Assert.Equal([second.AgentTask.Id, first.AgentTask.Id], descending.Items.Select(t => t.Id));
    }

    [Fact]
    public async Task List_PaginationBoundaries_RespectPageSizeAndPage()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        for (int i = 0; i < 3; i++)
        {
            await service.CreateAsync(ValidRequest(), CancellationToken.None);
        }

        PagedResult<AgentTask> page1 = await service.ListAsync(
            new AgentTaskListQuery(), new PaginationRequest(1, 2), CancellationToken.None);
        PagedResult<AgentTask> page2 = await service.ListAsync(
            new AgentTaskListQuery(), new PaginationRequest(2, 2), CancellationToken.None);

        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount >= 3);
        Assert.DoesNotContain(page1.Items.Select(t => t.Id), id => page2.Items.Any(t => t.Id == id));
    }

    [Fact]
    public async Task List_MyTasksOnly_ReturnsOnlyCallersTasks()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        Guid callerId = Guid.NewGuid();
        Guid otherOwnerId = Guid.NewGuid();
        AgentTaskService callerService = CreateService(context, new RecordingAuditRecorder(), callerId.ToString());
        AgentTaskService otherService = CreateService(
            context, new RecordingAuditRecorder(), callerId.ToString(),
            targetOwner: new StaffSubjectReference(otherOwnerId, true));
        AgentTaskMutationResult mine = await callerService.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskMutationResult theirs = await otherService.CreateAsync(
            ValidRequest() with { OwnerUserId = otherOwnerId }, CancellationToken.None);

        PagedResult<AgentTask> result = await callerService.ListAsync(
            new AgentTaskListQuery(MyTasksOnly: true), new PaginationRequest(1, 100), CancellationToken.None);

        Assert.Contains(result.Items, t => t.Id == mine.AgentTask!.Id);
        Assert.DoesNotContain(result.Items, t => t.Id == theirs.AgentTask!.Id);
        Assert.All(result.Items, t => Assert.Equal(callerId, t.OwnerUserId));
    }

    /// <summary>
    /// The owner id is resolved server-side; there is no client-suppliable
    /// owner-id parameter on the query at all, so there is nothing here to try
    /// to widen — this proves MyTasksOnly cannot be used to read another
    /// caller's tasks no matter what is created for other owners.
    /// </summary>
    [Fact]
    public async Task List_MyTasksOnly_NeverReturnsAnotherOwnersTasksRegardlessOfWhatElseExists()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        Guid callerId = Guid.NewGuid();
        Guid otherOwnerId = Guid.NewGuid();
        AgentTaskService otherService = CreateService(
            context, new RecordingAuditRecorder(), callerId.ToString(),
            targetOwner: new StaffSubjectReference(otherOwnerId, true));
        await otherService.CreateAsync(ValidRequest() with { OwnerUserId = otherOwnerId }, CancellationToken.None);
        AgentTaskService callerService = CreateService(context, new RecordingAuditRecorder(), callerId.ToString());

        PagedResult<AgentTask> result = await callerService.ListAsync(
            new AgentTaskListQuery(MyTasksOnly: true), new PaginationRequest(1, 100), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>Fail-closed: an unusable caller handle yields an empty page, never the whole list.</summary>
    [Fact]
    public async Task List_MyTasksOnly_WithUnusableCallerHandle_ReturnsEmptyPage()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService seedService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        await seedService.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), handle: null);

        PagedResult<AgentTask> result = await service.ListAsync(
            new AgentTaskListQuery(MyTasksOnly: true), new PaginationRequest(1, 100), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Get_NotOwner_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService ownerService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await ownerService.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskService otherService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await otherService.GetAsync(created.AgentTask!.Id, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotOwner, result.Failure);
    }

    [Fact]
    public async Task Get_Owner_Succeeds()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        string ownerHandle = Guid.NewGuid().ToString();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), ownerHandle);
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        AgentTaskMutationResult result = await service.GetAsync(created.AgentTask!.Id, CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(created.AgentTask.Id, result.AgentTask!.Id);
    }

    [Fact]
    public async Task Update_NotOwner_Fails_AndLeavesTaskUnchanged()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService ownerService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await ownerService.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskService otherService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await otherService.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("Changed title", null, null, null, null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotOwner, result.Failure);
        AgentTask stored = await context.AgentTasks.AsNoTracking().SingleAsync(task => task.Id == created.AgentTask.Id);
        Assert.Equal("Original title", stored.Title);
    }

    [Fact]
    public async Task Update_Owner_Succeeds_AndBumpsVersion()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        AgentTaskMutationResult result = await service.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("Updated title", "Updated details", null, null, null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal("Updated title", result.AgentTask!.Title);
        Assert.Equal(2, result.AgentTask.Version);
    }

    [Fact]
    public async Task Update_UnknownTicketId_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString(), ticketExists: false);
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        AgentTaskMutationResult result = await service.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("Original title", null, Guid.NewGuid(), null, null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.InvalidTicket, result.Failure);
    }

    [Fact]
    public async Task Update_UnknownCustomerId_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString(), customerExists: false);
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        AgentTaskMutationResult result = await service.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("Original title", null, null, Guid.NewGuid(), null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.InvalidCustomer, result.Failure);
    }

    [Fact]
    public async Task Update_StaleVersion_Fails_AndLeavesTaskUnchanged()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("First update", null, null, null, null, 1),
            CancellationToken.None);

        AgentTaskMutationResult result = await service.UpdateAsync(
            created.AgentTask.Id,
            new UpdateAgentTaskRequest("Second update", null, null, null, null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.StaleVersion, result.Failure);
        AgentTask stored = await context.AgentTasks.AsNoTracking().SingleAsync(task => task.Id == created.AgentTask.Id);
        Assert.Equal("First update", stored.Title);
    }

    [Fact]
    public async Task Update_UnknownTask_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateAgentTaskRequest("Title", null, null, null, null, 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.TaskNotFound, result.Failure);
    }

    [Fact]
    public async Task Complete_Succeeds_SetsStatusAndCompletedAtUtc_RecordsAudit_AndWritesOutboxMessage()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        AgentTaskService service = CreateService(context, auditRecorder, Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        auditRecorder.Requests.Clear();

        AgentTaskMutationResult result = await service.CompleteAsync(
            created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(AgentTaskStatus.Completed, result.AgentTask!.Status);
        Assert.NotNull(result.AgentTask.CompletedAtUtc);
        Assert.Equal(2, result.AgentTask.Version);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "completed" && request.EntityId == created.AgentTask.Id.ToString());

        // The row is updated in place, never deleted (AC).
        AgentTask stored = await context.AgentTasks.AsNoTracking().SingleAsync(task => task.Id == created.AgentTask.Id);
        Assert.Equal(AgentTaskStatus.Completed, stored.Status);

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "agent-task-management.agent-task-completed.v1"
                && message.Payload.Contains(created.AgentTask.Id.ToString()));
        Assert.Contains(created.AgentTask.Id.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Complete_NotOwner_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService ownerService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await ownerService.CreateAsync(ValidRequest(), CancellationToken.None);
        AgentTaskService otherService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await otherService.CompleteAsync(
            created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotOwner, result.Failure);
    }

    [Fact]
    public async Task Complete_StaleVersion_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.UpdateAsync(
            created.AgentTask!.Id,
            new UpdateAgentTaskRequest("Title", null, null, null, null, 1),
            CancellationToken.None);

        AgentTaskMutationResult result = await service.CompleteAsync(
            created.AgentTask.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.StaleVersion, result.Failure);
    }

    [Fact]
    public async Task Complete_AlreadyCompleted_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.CompleteAsync(created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        AgentTaskMutationResult result = await service.CompleteAsync(
            created.AgentTask.Id, new AgentTaskVersionedActionRequest(2), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.AlreadyCompleted, result.Failure);
    }

    [Fact]
    public async Task Complete_UnknownTask_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await service.CompleteAsync(
            Guid.NewGuid(), new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.TaskNotFound, result.Failure);
    }

    [Fact]
    public async Task Reopen_Succeeds_ClearsCompletedAtUtc_RecordsAudit_AndWritesOutboxMessage()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        AgentTaskService service = CreateService(context, auditRecorder, Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.CompleteAsync(created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);
        auditRecorder.Requests.Clear();

        AgentTaskMutationResult result = await service.ReopenAsync(
            created.AgentTask.Id, new AgentTaskVersionedActionRequest(2), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(AgentTaskStatus.Open, result.AgentTask!.Status);
        Assert.Null(result.AgentTask.CompletedAtUtc);
        Assert.Equal(3, result.AgentTask.Version);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "reopened" && request.EntityId == created.AgentTask.Id.ToString());

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "agent-task-management.agent-task-reopened.v1"
                && message.Payload.Contains(created.AgentTask.Id.ToString()));
        Assert.Contains(created.AgentTask.Id.ToString(), outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reopen_NotOwner_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService ownerService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await ownerService.CreateAsync(ValidRequest(), CancellationToken.None);
        await ownerService.CompleteAsync(created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);
        AgentTaskService otherService = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await otherService.ReopenAsync(
            created.AgentTask.Id, new AgentTaskVersionedActionRequest(2), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotOwner, result.Failure);
    }

    [Fact]
    public async Task Reopen_StaleVersion_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);
        await service.CompleteAsync(created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        AgentTaskMutationResult result = await service.ReopenAsync(
            created.AgentTask.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.StaleVersion, result.Failure);
    }

    [Fact]
    public async Task Reopen_NotCompleted_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTaskMutationResult created = await service.CreateAsync(ValidRequest(), CancellationToken.None);

        AgentTaskMutationResult result = await service.ReopenAsync(
            created.AgentTask!.Id, new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotCompleted, result.Failure);
    }

    [Fact]
    public async Task Reopen_UnknownTask_Fails()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await service.ReopenAsync(
            Guid.NewGuid(), new AgentTaskVersionedActionRequest(1), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.TaskNotFound, result.Failure);
    }

    private static CreateAgentTaskRequest ValidRequest() => new(
        "Original title", "Original details", null, null, null, null);

    private static AgentTaskService CreateService(
        AgentTaskManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle,
        bool customerExists = true,
        bool ticketExists = true,
        StaffSubjectReference? targetOwner = null) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubCustomerExistsLookup(customerExists),
            new StubTicketExistsLookup(ticketExists),
            new StubStaffSubjectReferenceReader(targetOwner));

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

    private sealed class StubTicketExistsLookup(bool exists) : ITicketExistsLookup
    {
        public Task<bool> ExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(exists);
    }

    /// <summary>
    /// Returns the configured subject for ANY id — the owner-eligibility tests
    /// care about the active/unknown distinction, not about id matching.
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
