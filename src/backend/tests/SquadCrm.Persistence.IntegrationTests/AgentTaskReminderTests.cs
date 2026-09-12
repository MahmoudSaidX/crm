using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.AgentTaskManagement;
using SquadCrm.Modules.AgentTaskManagement.Persistence;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.TicketManagement.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Agent task reminders (CRM-144): set/update/clear, eligibility, and the
/// due-reminder sweep's supersession and idempotency guarantees.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class AgentTaskReminderTests
{
    private const string ReminderDueContract = "agent-task-management.agent-task-reminder-due.v1";

    public AgentTaskReminderTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task SetReminder_SchedulesInUtc_BumpsVersion_AndRecordsAudit()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        Guid callerId = Guid.NewGuid();
        AgentTaskService service = CreateService(context, auditRecorder, callerId.ToString());
        AgentTask task = await CreateTaskAsync(service);
        DateTimeOffset reminderAt = DateTimeOffset.UtcNow.AddHours(3);
        // Captured up front: `task` is the SAME tracked instance the service
        // mutates, so reading Version after the call would read the new one.
        int versionBefore = task.Version;

        AgentTaskMutationResult result = await service.SetReminderAsync(
            task.Id, new SetAgentTaskReminderRequest(reminderAt, versionBefore), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(AgentTaskReminderStatus.Scheduled, result.AgentTask!.ReminderStatus);
        Assert.NotNull(result.AgentTask.ReminderEventId);
        Assert.Null(result.AgentTask.ReminderTriggeredAtUtc);
        Assert.Equal(reminderAt.ToUniversalTime(), result.AgentTask.ReminderAtUtc!.Value.ToUniversalTime());
        // AC: persisted consistently in UTC regardless of the caller's offset.
        Assert.Equal(TimeSpan.Zero, result.AgentTask.ReminderAtUtc!.Value.Offset);
        Assert.Equal(versionBefore + 1, result.AgentTask.Version);
        Assert.Single(auditRecorder.Requests, request => request.Action == "reminder_set");
    }

    /// <summary>
    /// AC: "reminder is ... persisted/scheduled consistently in UTC" — a
    /// caller supplying a non-UTC offset must not shift the scheduled instant.
    /// </summary>
    [Fact]
    public async Task SetReminder_NonUtcOffset_PreservesTheSameInstant()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        DateTimeOffset reminderAt = new(DateTime.UtcNow.AddHours(5).Ticks, TimeSpan.Zero);
        DateTimeOffset inTehranOffset = reminderAt.ToOffset(TimeSpan.FromHours(3.5));

        AgentTaskMutationResult result = await service.SetReminderAsync(
            task.Id, new SetAgentTaskReminderRequest(inTehranOffset, task.Version), CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Equal(reminderAt, result.AgentTask!.ReminderAtUtc!.Value.ToUniversalTime());
    }

    /// <summary>
    /// AC: "rescheduling a reminder cancels/supersedes the previous scheduled
    /// occurrence without duplicate alerts" — the occurrence identity changes,
    /// so the replaced reminder can never also fire.
    /// </summary>
    [Fact]
    public async Task SetReminder_Reschedule_SupersedesThePreviousOccurrenceIdentity()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);

        AgentTaskMutationResult first = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), task.Version),
            CancellationToken.None);
        Guid firstEventId = first.AgentTask!.ReminderEventId!.Value;

        AgentTaskMutationResult second = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(2), first.AgentTask.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, second.Failure);
        Assert.NotEqual(firstEventId, second.AgentTask!.ReminderEventId!.Value);
        Assert.Equal(AgentTaskReminderStatus.Scheduled, second.AgentTask.ReminderStatus);
    }

    [Fact]
    public async Task ClearReminder_RemovesTheScheduledOccurrence()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        AgentTaskService service = CreateService(context, auditRecorder, Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult scheduled = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), task.Version),
            CancellationToken.None);

        AgentTaskMutationResult result = await service.ClearReminderAsync(
            task.Id,
            new AgentTaskVersionedActionRequest(scheduled.AgentTask!.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, result.Failure);
        Assert.Null(result.AgentTask!.ReminderAtUtc);
        Assert.Null(result.AgentTask.ReminderEventId);
        Assert.Equal(AgentTaskReminderStatus.None, result.AgentTask.ReminderStatus);
        Assert.Single(auditRecorder.Requests, request => request.Action == "reminder_cleared");
    }

    [Fact]
    public async Task SetReminder_StaleVersion_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);

        AgentTaskMutationResult result = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), task.Version - 1),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.StaleVersion, result.Failure);
    }

    [Fact]
    public async Task SetReminder_ByNonOwner_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService ownerService = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(ownerService);
        AgentTaskService intruderService = CreateService(
            context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());

        AgentTaskMutationResult result = await intruderService.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), task.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.NotOwner, result.Failure);
    }

    [Fact]
    public async Task SetReminder_InThePast_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);

        AgentTaskMutationResult result = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddMinutes(-1), task.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.ReminderInPast, result.Failure);
    }

    /// <summary>AC: completed/ineligible tasks do not take (or generate) reminders.</summary>
    [Fact]
    public async Task SetReminder_OnCompletedTask_IsRejected()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult completed = await service.CompleteAsync(
            task.Id, new AgentTaskVersionedActionRequest(task.Version), CancellationToken.None);

        AgentTaskMutationResult result = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), completed.AgentTask!.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.ReminderTaskNotOpen, result.Failure);
    }

    /// <summary>AC: completed tasks do not generate future reminders.</summary>
    [Fact]
    public async Task Complete_CancelsAScheduledReminder()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult scheduled = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddHours(1), task.Version),
            CancellationToken.None);

        AgentTaskMutationResult completed = await service.CompleteAsync(
            task.Id,
            new AgentTaskVersionedActionRequest(scheduled.AgentTask!.Version),
            CancellationToken.None);

        Assert.Equal(AgentTaskMutationFailure.None, completed.Failure);
        Assert.Equal(AgentTaskReminderStatus.Cancelled, completed.AgentTask!.ReminderStatus);
        Assert.Null(completed.AgentTask.ReminderEventId);
    }

    /// <summary>
    /// The core sweep behaviour: exactly one durable reminder event, keyed by
    /// the reminder's own occurrence id, written in the same transaction as
    /// the Scheduled -> Triggered transition.
    /// </summary>
    [Fact]
    public async Task Sweep_DueReminder_TriggersOnceAndWritesTheDurableEvent()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult scheduled = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddMinutes(30), task.Version),
            CancellationToken.None);
        Guid reminderEventId = scheduled.AgentTask!.ReminderEventId!.Value;

        int fired = await CreateReminderService(context)
            .TriggerDueRemindersAsync(DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);

        Assert.Equal(1, fired);
        AgentTask reloaded = await context.AgentTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(AgentTaskReminderStatus.Triggered, reloaded.ReminderStatus);
        Assert.NotNull(reloaded.ReminderTriggeredAtUtc);
        // BR: firing must not change the task's own completion state...
        Assert.Equal(AgentTaskStatus.Open, reloaded.Status);
        // ...nor invalidate the version the owner is holding in an open form.
        Assert.Equal(scheduled.AgentTask.Version, reloaded.Version);

        OutboxMessage message = await context.OutboxMessages.AsNoTracking()
            .SingleAsync(candidate => candidate.Type == ReminderDueContract);
        // The outbox row's identity IS the reminder occurrence id.
        Assert.Equal(reminderEventId, message.Id);
        Assert.Contains(task.Id.ToString(), message.Payload);
    }

    /// <summary>
    /// AC: "background processing retries are idempotent" — re-running the
    /// sweep produces no second reminder event for the same occurrence.
    /// </summary>
    [Fact]
    public async Task Sweep_RunTwice_DoesNotDuplicateTheReminderEvent()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddMinutes(30), task.Version),
            CancellationToken.None);
        DateTimeOffset sweepAt = DateTimeOffset.UtcNow.AddHours(1);

        int first = await CreateReminderService(context).TriggerDueRemindersAsync(sweepAt, CancellationToken.None);
        int second = await CreateReminderService(context).TriggerDueRemindersAsync(sweepAt, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        int reminderEvents = await context.OutboxMessages.AsNoTracking()
            .CountAsync(message => message.Type == ReminderDueContract && message.Payload.Contains(task.Id.ToString()));
        Assert.Equal(1, reminderEvents);
    }

    [Fact]
    public async Task Sweep_FutureReminder_IsNotTriggered()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddDays(2), task.Version),
            CancellationToken.None);

        await CreateReminderService(context).TriggerDueRemindersAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        AgentTask reloaded = await context.AgentTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        Assert.Equal(AgentTaskReminderStatus.Scheduled, reloaded.ReminderStatus);
        Assert.False(await context.OutboxMessages.AsNoTracking()
            .AnyAsync(message => message.Type == ReminderDueContract && message.Payload.Contains(task.Id.ToString())));
    }

    /// <summary>AC: a completed task generates no future reminder, even one already due.</summary>
    [Fact]
    public async Task Sweep_CompletedTaskWithADueReminder_IsNotTriggered()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult scheduled = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddMinutes(30), task.Version),
            CancellationToken.None);
        await service.CompleteAsync(
            task.Id,
            new AgentTaskVersionedActionRequest(scheduled.AgentTask!.Version),
            CancellationToken.None);

        int fired = await CreateReminderService(context)
            .TriggerDueRemindersAsync(DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);

        Assert.Equal(0, fired);
        Assert.False(await context.OutboxMessages.AsNoTracking()
            .AnyAsync(message => message.Type == ReminderDueContract && message.Payload.Contains(task.Id.ToString())));
    }

    [Fact]
    public async Task Sweep_ClearedReminder_IsNotTriggered()
    {
        await using AgentTaskManagementDbContext context = PostgresTestDatabase.CreateAgentTaskManagementContext();
        AgentTaskService service = CreateService(context, new RecordingAuditRecorder(), Guid.NewGuid().ToString());
        AgentTask task = await CreateTaskAsync(service);
        AgentTaskMutationResult scheduled = await service.SetReminderAsync(
            task.Id,
            new SetAgentTaskReminderRequest(DateTimeOffset.UtcNow.AddMinutes(30), task.Version),
            CancellationToken.None);
        await service.ClearReminderAsync(
            task.Id,
            new AgentTaskVersionedActionRequest(scheduled.AgentTask!.Version),
            CancellationToken.None);

        int fired = await CreateReminderService(context)
            .TriggerDueRemindersAsync(DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);

        Assert.Equal(0, fired);
    }

    private static async Task<AgentTask> CreateTaskAsync(AgentTaskService service)
    {
        AgentTaskMutationResult created = await service.CreateAsync(
            new CreateAgentTaskRequest("Follow up with the customer", null, null, null, null, null),
            CancellationToken.None);
        Assert.Equal(AgentTaskMutationFailure.None, created.Failure);
        return created.AgentTask!;
    }

    private static AgentTaskReminderService CreateReminderService(AgentTaskManagementDbContext context) =>
        new(context, NullLogger<AgentTaskReminderService>.Instance);

    private static AgentTaskService CreateService(
        AgentTaskManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubExistsLookup(),
            new StubExistsLookup(),
            new StubStaffSubjectReferenceReader());

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class StubExistsLookup : ICustomerExistsLookup, ITicketExistsLookup
    {
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubStaffSubjectReferenceReader : IStaffSubjectReferenceReader
    {
        public Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<StaffSubjectReference?>(null);

        public Task<StaffSubjectReference?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<StaffSubjectReference?>(null);
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
