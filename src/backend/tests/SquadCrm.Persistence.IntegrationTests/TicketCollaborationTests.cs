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

/// <summary>
/// Internal ticket collaboration — notes, mentions and watchers (CRM-147).
/// <para>
/// There are no handoff tests here on purpose: handoff is reassignment, which
/// <c>TicketManagementTests</c> already covers through
/// <c>TicketService.AssignAsync</c> (reason required when replacing an owner,
/// append-only history row, assignment event). This story adds no second
/// ownership path to test.
/// </para>
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class TicketCollaborationTests
{
    public TicketCollaborationTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task AddNote_Succeeds_PersistsMentions_RecordsAudit_AndWritesOneOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        Guid mentionedUserId = Guid.NewGuid();
        RecordingAuditRecorder auditRecorder = new();
        TicketCollaborationService service = CreateService(context, auditRecorder, "agent@example.test", ActiveStaffSubject);

        TicketNoteResult result = await service.AddNoteAsync(
            ticketId,
            new AddTicketNoteRequest("Checked the account; waiting on billing.", [mentionedUserId]),
            CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, result.Failure);
        Assert.NotNull(result.Note);
        Assert.Equal("Checked the account; waiting on billing.", result.Note!.Body);
        Assert.Equal("agent@example.test", result.Note.CreatedBy);
        Assert.Equal([mentionedUserId], result.Note.MentionedUserIds);

        Assert.Single(
            await context.TicketNoteMentions.AsNoTracking()
                .Where(mention => mention.NoteId == result.Note.Id)
                .ToListAsync());

        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "note_added" && request.EntityId == ticketId.ToString());

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Payload.Contains(result.Note.Id.ToString()));
        Assert.Equal("ticket-management.ticket-note-added.v1", outboxMessage.Type);

        // The note body must not travel on the durable event: the outbox is a
        // cross-module/external boundary and internal discussion stays internal
        // there too (AC 6).
        Assert.DoesNotContain("waiting on billing", outboxMessage.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddNote_TrimsBody()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        TicketNoteResult result = await service.AddNoteAsync(
            ticketId, new AddTicketNoteRequest("   padded note   "), CancellationToken.None);

        Assert.Equal("padded note", result.Note!.Body);
    }

    [Fact]
    public async Task AddNote_DeduplicatesRepeatedMention()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        Guid mentionedUserId = Guid.NewGuid();
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        TicketNoteResult result = await service.AddNoteAsync(
            ticketId,
            new AddTicketNoteRequest("Please take a look.", [mentionedUserId, mentionedUserId]),
            CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, result.Failure);

        // One mention row, therefore one future notification, not two.
        Assert.Single(
            await context.TicketNoteMentions.AsNoTracking()
                .Where(mention => mention.NoteId == result.Note!.Id)
                .ToListAsync());
    }

    [Fact]
    public async Task AddNote_InactiveMentionedUser_RejectsWholeNote()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(
            context,
            new RecordingAuditRecorder(),
            "agent@example.test",
            staffSubject: new StaffSubjectReference(Guid.NewGuid(), IsActive: false));

        TicketNoteResult result = await service.AddNoteAsync(
            ticketId,
            new AddTicketNoteRequest("Take a look.", [Guid.NewGuid()]),
            CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.IneligibleUser, result.Failure);

        // Nothing is written — an ineligible mention must not leave a note
        // behind that silently dropped it (BR). Scoped to this ticket: the
        // suite shares one database.
        Assert.Empty(await context.TicketInternalNotes.AsNoTracking()
            .Where(note => note.TicketId == ticketId).ToListAsync());
    }

    [Fact]
    public async Task AddNote_UnknownMentionedUser_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", staffSubject: null);

        TicketNoteResult result = await service.AddNoteAsync(
            ticketId, new AddTicketNoteRequest("Take a look.", [Guid.NewGuid()]), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.IneligibleUser, result.Failure);
    }

    [Fact]
    public async Task AddNote_UnknownTicket_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        TicketNoteResult result = await service.AddNoteAsync(
            Guid.NewGuid(), new AddTicketNoteRequest("Orphan note."), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.TicketNotFound, result.Failure);
    }

    [Fact]
    public async Task ListNotes_ReturnsNewestFirst()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        await service.AddNoteAsync(ticketId, new AddTicketNoteRequest("first"), CancellationToken.None);
        await service.AddNoteAsync(ticketId, new AddTicketNoteRequest("second"), CancellationToken.None);

        TicketCollaborationListResult<TicketInternalNoteResponse> result =
            await service.ListNotesAsync(ticketId, new PaginationRequest(), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, result.Failure);
        Assert.Equal(2, result.Page!.TotalCount);
        Assert.Equal("second", result.Page.Items[0].Body);
        Assert.Equal("first", result.Page.Items[1].Body);
    }

    [Fact]
    public async Task AddWatcher_Succeeds_WritesHistory_RecordsAudit_AndWritesOneOutboxMessage()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        Guid userId = Guid.NewGuid();
        RecordingAuditRecorder auditRecorder = new();
        TicketCollaborationService service = CreateService(context, auditRecorder, "agent@example.test", ActiveStaffSubject);

        TicketWatcherResult result = await service.AddWatcherAsync(
            ticketId, new AddTicketWatcherRequest(userId), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, result.Failure);
        Assert.Equal(userId, result.Watcher!.UserId);

        TicketWatcherHistory history = await context.TicketWatcherHistory
            .AsNoTracking()
            .SingleAsync(entry => entry.TicketId == ticketId);
        Assert.Equal(TicketWatcherAction.Added, history.Action);

        Assert.Single(auditRecorder.Requests, request => request.Action == "watcher_added");

        OutboxMessage outboxMessage = await context.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Type == "ticket-management.ticket-watcher-changed.v1"
                && message.Payload.Contains(ticketId.ToString()));
        Assert.Contains("Added", outboxMessage.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddWatcher_Twice_IsNoOp()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        Guid userId = Guid.NewGuid();
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        await service.AddWatcherAsync(ticketId, new AddTicketWatcherRequest(userId), CancellationToken.None);
        TicketWatcherResult second = await service.AddWatcherAsync(
            ticketId, new AddTicketWatcherRequest(userId), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, second.Failure);

        // One membership row, one history row and one event: a double-submit
        // cannot notify the same watcher twice.
        Assert.Single(await context.TicketWatchers.AsNoTracking().Where(w => w.TicketId == ticketId).ToListAsync());
        Assert.Single(await context.TicketWatcherHistory.AsNoTracking().Where(h => h.TicketId == ticketId).ToListAsync());
        Assert.Single(await context.OutboxMessages.AsNoTracking()
            .Where(m => m.Type == "ticket-management.ticket-watcher-changed.v1"
                && m.Payload.Contains(ticketId.ToString())).ToListAsync());
    }

    [Fact]
    public async Task AddWatcher_InactiveUser_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(
            context,
            new RecordingAuditRecorder(),
            "agent@example.test",
            staffSubject: new StaffSubjectReference(Guid.NewGuid(), IsActive: false));

        TicketWatcherResult result = await service.AddWatcherAsync(
            ticketId, new AddTicketWatcherRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.IneligibleUser, result.Failure);
        Assert.Empty(await context.TicketWatchers.AsNoTracking()
            .Where(watcher => watcher.TicketId == ticketId).ToListAsync());
    }

    [Fact]
    public async Task RemoveWatcher_DeletesMembership_ButPreservesHistory()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        Guid userId = Guid.NewGuid();
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);
        await service.AddWatcherAsync(ticketId, new AddTicketWatcherRequest(userId), CancellationToken.None);

        TicketCollaborationFailure failure = await service.RemoveWatcherAsync(
            ticketId, userId, CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.None, failure);
        Assert.Empty(await context.TicketWatchers.AsNoTracking().Where(w => w.TicketId == ticketId).ToListAsync());

        // Both the add and the remove survive: the timeline must still show
        // that someone was removed, and by whom (BR auditability).
        List<TicketWatcherHistory> history = await context.TicketWatcherHistory
            .AsNoTracking().Where(entry => entry.TicketId == ticketId).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, entry => entry.Action == TicketWatcherAction.Removed);

        Assert.Equal(
            2,
            await context.OutboxMessages.AsNoTracking()
                .CountAsync(m => m.Type == "ticket-management.ticket-watcher-changed.v1"
                    && m.Payload.Contains(ticketId.ToString())));
    }

    [Fact]
    public async Task RemoveWatcher_NotWatching_IsRejected()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);

        TicketCollaborationFailure failure = await service.RemoveWatcherAsync(
            ticketId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(TicketCollaborationFailure.WatcherNotFound, failure);
    }

    [Fact]
    public async Task Timeline_ShowsCollaborationToAgents_ButNeverToCustomers()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService collaboration = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);
        await collaboration.AddNoteAsync(
            ticketId,
            new AddTicketNoteRequest("Customer is upset; handle carefully.", [Guid.NewGuid()]),
            CancellationToken.None);
        await collaboration.AddWatcherAsync(
            ticketId, new AddTicketWatcherRequest(Guid.NewGuid()), CancellationToken.None);

        TicketTimelineService timeline = new(context);

        TicketTimelineResult internalTimeline = await timeline.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);
        Assert.Contains(internalTimeline.Page!.Items, entry => entry.EventType == "TicketNoteAdded");
        Assert.Contains(internalTimeline.Page.Items, entry => entry.EventType == "TicketWatcherAdded");

        TicketTimelineResult customerTimeline = await timeline.GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Customer, CancellationToken.None);
        Assert.DoesNotContain(customerTimeline.Page!.Items, entry => entry.EventType == "TicketNoteAdded");
        Assert.DoesNotContain(customerTimeline.Page.Items, entry => entry.EventType == "TicketWatcherAdded");

        // Belt and braces: the note text must not reach a customer through any
        // entry's summary or reason, however the projection is later changed.
        Assert.DoesNotContain(
            customerTimeline.Page.Items,
            entry => entry.Summary.Contains("upset", StringComparison.OrdinalIgnoreCase)
                || (entry.Reason?.Contains("upset", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    [Fact]
    public async Task Timeline_NoteSummary_NeverCarriesTheNoteBody()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        Guid ticketId = await SeedTicketAsync(context);
        TicketCollaborationService collaboration = CreateService(
            context, new RecordingAuditRecorder(), "agent@example.test", ActiveStaffSubject);
        await collaboration.AddNoteAsync(
            ticketId,
            new AddTicketNoteRequest("Internal password reset workaround applied."),
            CancellationToken.None);

        TicketTimelineResult result = await new TicketTimelineService(context).GetAsync(
            ticketId, new PaginationRequest(), TicketTimelineAudience.Internal, CancellationToken.None);

        TicketTimelineEntryResponse entry = Assert.Single(
            result.Page!.Items, candidate => candidate.EventType == "TicketNoteAdded");
        Assert.DoesNotContain("password", entry.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Guid> SeedTicketAsync(TicketManagementDbContext context)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TicketCategory category = new()
        {
            Id = Guid.NewGuid(),
            Code = $"CAT-{Guid.NewGuid():N}"[..12],
            NormalizedCode = $"CAT-{Guid.NewGuid():N}"[..12],
            ArabicName = "فئة",
            EnglishName = "Category",
            IsActive = true,
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
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        context.TicketCategories.Add(category);
        context.TicketPriorities.Add(priority);
        await context.SaveChangesAsync();

        TicketService ticketService = new(
            context,
            new StubCurrentUserAccessor("agent@example.test"),
            new RecordingAuditRecorder(),
            new StubCustomerExistsLookup(),
            new StubDepartmentActiveLookup(),
            new StubBranchActiveLookup(),
            new StubStaffSubjectReferenceReader(new StaffSubjectReference(Guid.NewGuid(), IsActive: true)));

        TicketMutationResult created = await ticketService.CreateAsync(
            new CreateTicketRequest(
                Guid.NewGuid(),
                "Cannot log in",
                "The customer cannot log in to the portal.",
                category.Id,
                null,
                priority.Id,
                Guid.NewGuid(),
                Guid.NewGuid(),
                TicketChannel.Agent,
                null),
            CancellationToken.None);

        return created.Ticket!.Id;
    }

    /// <summary>
    /// <paramref name="staffSubject"/> is what the stub reader returns for ANY
    /// id. It is NOT defaulted with <c>??</c>: null is a meaningful value here
    /// — it models an unknown user — so the caller passes
    /// <see cref="ActiveStaffSubject"/> explicitly for the eligible case.
    /// </summary>
    private static TicketCollaborationService CreateService(
        TicketManagementDbContext context,
        IAuditRecorder auditRecorder,
        string? handle,
        StaffSubjectReference? staffSubject) =>
        new(
            context,
            new StubCurrentUserAccessor(handle),
            auditRecorder,
            new StubStaffSubjectReferenceReader(staffSubject));

    private static StaffSubjectReference ActiveStaffSubject =>
        new(Guid.NewGuid(), IsActive: true);

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class StubCustomerExistsLookup : ICustomerExistsLookup
    {
        public Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class StubDepartmentActiveLookup : IDepartmentActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class StubBranchActiveLookup : IBranchActiveLookup
    {
        public Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    /// <summary>Returns the configured subject for ANY id — these tests care
    /// about the active/unknown distinction, not about id matching.</summary>
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
