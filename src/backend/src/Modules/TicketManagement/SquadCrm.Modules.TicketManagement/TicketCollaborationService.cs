using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

/// <summary>Discriminates why a collaboration call did not succeed.</summary>
public enum TicketCollaborationFailure
{
    None,
    TicketNotFound,

    /// <summary>A mentioned or watched user is unknown or not an active staff user.</summary>
    IneligibleUser,

    /// <summary>Removal targeted a user who is not watching the ticket.</summary>
    WatcherNotFound,
}

public readonly record struct TicketNoteResult(
    TicketInternalNoteResponse? Note, TicketCollaborationFailure Failure)
{
    public static TicketNoteResult Success(TicketInternalNoteResponse note) =>
        new(note, TicketCollaborationFailure.None);

    public static TicketNoteResult Failed(TicketCollaborationFailure failure) => new(null, failure);
}

public readonly record struct TicketWatcherResult(
    TicketWatcherResponse? Watcher, TicketCollaborationFailure Failure)
{
    public static TicketWatcherResult Success(TicketWatcherResponse watcher) =>
        new(watcher, TicketCollaborationFailure.None);

    public static TicketWatcherResult Failed(TicketCollaborationFailure failure) => new(null, failure);
}

public readonly record struct TicketCollaborationListResult<T>(
    PagedResult<T>? Page, TicketCollaborationFailure Failure)
{
    public static TicketCollaborationListResult<T> Success(PagedResult<T> page) =>
        new(page, TicketCollaborationFailure.None);

    public static TicketCollaborationListResult<T> Failed(TicketCollaborationFailure failure) =>
        new(null, failure);
}

/// <summary>
/// Internal ticket collaboration — notes, mentions and watchers (CRM-147).
/// <para>
/// <b>Handoff is deliberately absent from this service.</b> Handing a ticket to
/// another agent is reassignment, which <see cref="TicketService.AssignAsync"/>
/// already owns: it validates the target agent, requires a reason when an
/// existing owner is replaced, appends a <see cref="TicketAssignmentHistory"/>
/// row and raises the assignment event. Department handoff is escalation
/// (<see cref="TicketService.EscalateAsync"/> with
/// <see cref="TicketEscalationTargetType.Department"/>). Adding a handoff path
/// here would create the competing ownership model the Business Rules forbid.
/// </para>
/// <para>
/// Everything this service writes is internal by construction — there is no
/// customer-visible projection of a note or a watcher list anywhere, and the
/// timeline arms that surface them are marked
/// <see cref="TicketTimelineVisibility.Internal"/>, which the customer audience
/// filter drops (AC 6).
/// </para>
/// </summary>
internal sealed class TicketCollaborationService(
    TicketManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    IStaffSubjectReferenceReader staffSubjectReferenceReader)
{
    public async Task<TicketNoteResult> AddNoteAsync(
        Guid ticketId,
        AddTicketNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TicketExistsAsync(ticketId, cancellationToken))
        {
            return TicketNoteResult.Failed(TicketCollaborationFailure.TicketNotFound);
        }

        // Distinct first: a client repeating the same id must not produce two
        // mention rows and therefore two notifications. The unique index backs
        // this up against a concurrent request.
        List<Guid> mentionedUserIds = (request.MentionedUserIds ?? []).Distinct().ToList();

        // Every mentioned user is validated before anything is written, and one
        // ineligible id rejects the WHOLE note rather than being silently
        // dropped: a partially-applied mention list would leave the author
        // believing a teammate was notified when they were not (BR "a mention
        // cannot bypass authorization").
        foreach (Guid mentionedUserId in mentionedUserIds)
        {
            if (!await IsEligibleStaffUserAsync(mentionedUserId, cancellationToken))
            {
                return TicketNoteResult.Failed(TicketCollaborationFailure.IneligibleUser);
            }
        }

        DateTimeOffset createdAtUtc = DateTimeOffset.UtcNow;
        string createdBy = currentUserAccessor.Handle ?? "unknown";
        TicketInternalNote note = TicketInternalNote.Create(
            Guid.NewGuid(),
            ticketId,
            request.Body.Trim(),
            createdBy,
            mentionedUserIds,
            createdAtUtc);

        dbContext.TicketInternalNotes.Add(note);

        // Same change tracker, therefore the same transaction as the note and
        // the outbox row: note, mentions and event commit together or not at
        // all, which is what makes the mention notification durable (AC).
        dbContext.TicketNoteMentions.AddRange(mentionedUserIds.Select(mentionedUserId =>
            new TicketNoteMention
            {
                Id = Guid.NewGuid(),
                NoteId = note.Id,
                TicketId = ticketId,
                MentionedUserId = mentionedUserId,
                CreatedAtUtc = createdAtUtc,
            }));

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(ticketId, "note_added", cancellationToken);
        return TicketNoteResult.Success(new TicketInternalNoteResponse(
            note.Id, ticketId, note.Body, note.CreatedBy, note.CreatedAtUtc, mentionedUserIds));
    }

    public async Task<TicketCollaborationListResult<TicketInternalNoteResponse>> ListNotesAsync(
        Guid ticketId,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        if (!await TicketExistsAsync(ticketId, cancellationToken))
        {
            return TicketCollaborationListResult<TicketInternalNoteResponse>.Failed(
                TicketCollaborationFailure.TicketNotFound);
        }

        IQueryable<TicketInternalNote> query = dbContext.TicketInternalNotes
            .AsNoTracking()
            .Where(note => note.TicketId == ticketId);

        int totalCount = await query.CountAsync(cancellationToken);

        // Newest first: an agent opening a ticket wants the latest internal
        // context, unlike the timeline which reads oldest-first as a narrative.
        // Id is a total tie-breaker so two notes written in the same tick keep
        // a stable relative order and pages never overlap or skip one.
        List<TicketInternalNote> notes = await query
            .OrderByDescending(note => note.CreatedAtUtc)
            .ThenBy(note => note.Id)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        // One query for the whole page's mentions rather than one per note.
        List<Guid> noteIds = notes.Select(note => note.Id).ToList();
        List<TicketNoteMention> mentions = await dbContext.TicketNoteMentions
            .AsNoTracking()
            .Where(mention => noteIds.Contains(mention.NoteId))
            .ToListAsync(cancellationToken);

        List<TicketInternalNoteResponse> items = notes
            .Select(note => new TicketInternalNoteResponse(
                note.Id,
                note.TicketId,
                note.Body,
                note.CreatedBy,
                note.CreatedAtUtc,
                mentions
                    .Where(mention => mention.NoteId == note.Id)
                    .Select(mention => mention.MentionedUserId)
                    .ToList()))
            .ToList();

        return TicketCollaborationListResult<TicketInternalNoteResponse>.Success(
            new PagedResult<TicketInternalNoteResponse>(
                items, pagination.Page, pagination.PageSize, totalCount));
    }

    public async Task<TicketWatcherResult> AddWatcherAsync(
        Guid ticketId,
        AddTicketWatcherRequest request,
        CancellationToken cancellationToken)
    {
        if (!await TicketExistsAsync(ticketId, cancellationToken))
        {
            return TicketWatcherResult.Failed(TicketCollaborationFailure.TicketNotFound);
        }

        if (!await IsEligibleStaffUserAsync(request.UserId, cancellationToken))
        {
            return TicketWatcherResult.Failed(TicketCollaborationFailure.IneligibleUser);
        }

        TicketWatcher? existing = await dbContext.TicketWatchers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                watcher => watcher.TicketId == ticketId && watcher.UserId == request.UserId,
                cancellationToken);

        // Membership is a set, so re-adding is a successful no-op: no second
        // row, no history entry and no second event, which means a double
        // -submit cannot notify the same watcher twice.
        if (existing is not null)
        {
            return TicketWatcherResult.Success(new TicketWatcherResponse(
                existing.UserId, existing.AddedBy, existing.AddedAtUtc));
        }

        DateTimeOffset addedAtUtc = DateTimeOffset.UtcNow;
        string addedBy = currentUserAccessor.Handle ?? "unknown";
        TicketWatcher watcher = TicketWatcher.Create(
            Guid.NewGuid(), ticketId, request.UserId, addedBy, addedAtUtc);

        dbContext.TicketWatchers.Add(watcher);
        dbContext.TicketWatcherHistory.Add(new TicketWatcherHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = request.UserId,
            Action = TicketWatcherAction.Added,
            ChangedBy = addedBy,
            ChangedAtUtc = addedAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(ticketId, "watcher_added", cancellationToken);
        return TicketWatcherResult.Success(new TicketWatcherResponse(
            watcher.UserId, watcher.AddedBy, watcher.AddedAtUtc));
    }

    public async Task<TicketCollaborationFailure> RemoveWatcherAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await TicketExistsAsync(ticketId, cancellationToken))
        {
            return TicketCollaborationFailure.TicketNotFound;
        }

        // Tracked, not AsNoTracking: MarkRemoved raises the removal event on
        // this instance, and the interceptor only drains TRACKED entities.
        TicketWatcher? watcher = await dbContext.TicketWatchers.SingleOrDefaultAsync(
            candidate => candidate.TicketId == ticketId && candidate.UserId == userId,
            cancellationToken);
        if (watcher is null)
        {
            return TicketCollaborationFailure.WatcherNotFound;
        }

        DateTimeOffset removedAtUtc = DateTimeOffset.UtcNow;
        string removedBy = currentUserAccessor.Handle ?? "unknown";
        watcher.MarkRemoved(removedBy, removedAtUtc);

        dbContext.TicketWatchers.Remove(watcher);

        // The membership row is deleted, so without this append-only row the
        // fact that someone was removed — and by whom — would disappear from
        // the ticket timeline (AC 5, and the auditability BR).
        dbContext.TicketWatcherHistory.Add(new TicketWatcherHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UserId = userId,
            Action = TicketWatcherAction.Removed,
            ChangedBy = removedBy,
            ChangedAtUtc = removedAtUtc,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(ticketId, "watcher_removed", cancellationToken);
        return TicketCollaborationFailure.None;
    }

    public async Task<TicketCollaborationListResult<TicketWatcherResponse>> ListWatchersAsync(
        Guid ticketId,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        if (!await TicketExistsAsync(ticketId, cancellationToken))
        {
            return TicketCollaborationListResult<TicketWatcherResponse>.Failed(
                TicketCollaborationFailure.TicketNotFound);
        }

        IQueryable<TicketWatcher> query = dbContext.TicketWatchers
            .AsNoTracking()
            .Where(watcher => watcher.TicketId == ticketId);

        int totalCount = await query.CountAsync(cancellationToken);

        List<TicketWatcherResponse> items = await query
            .OrderBy(watcher => watcher.AddedAtUtc)
            .ThenBy(watcher => watcher.Id)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(watcher => new TicketWatcherResponse(
                watcher.UserId, watcher.AddedBy, watcher.AddedAtUtc))
            .ToListAsync(cancellationToken);

        return TicketCollaborationListResult<TicketWatcherResponse>.Success(
            new PagedResult<TicketWatcherResponse>(
                items, pagination.Page, pagination.PageSize, totalCount));
    }

    private Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        dbContext.Tickets.AsNoTracking().AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);

    /// <summary>
    /// A mentioned or watched user must be a real, active staff user. This is
    /// the enforcement point for the Business Rule that collaboration cannot
    /// reach someone outside the organization — an arbitrary GUID from a client
    /// is rejected here rather than stored and later notified.
    /// </summary>
    private async Task<bool> IsEligibleStaffUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        StaffSubjectReference? user = await staffSubjectReferenceReader.FindByIdAsync(userId, cancellationToken);
        return user is { IsActive: true };
    }

    private Task RecordAuditAsync(Guid ticketId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "Ticket", ticketId.ToString(), Metadata: null),
            cancellationToken);
}
