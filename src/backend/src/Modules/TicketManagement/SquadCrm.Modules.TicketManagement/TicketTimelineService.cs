using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

/// <summary>Discriminates why a timeline request did not succeed.</summary>
public enum TicketTimelineFailure
{
    None,
    TicketNotFound,
}

public readonly record struct TicketTimelineResult(
    PagedResult<TicketTimelineEntryResponse>? Page, TicketTimelineFailure Failure)
{
    public static TicketTimelineResult Success(PagedResult<TicketTimelineEntryResponse> page) =>
        new(page, TicketTimelineFailure.None);

    public static TicketTimelineResult Failed(TicketTimelineFailure failure) => new(null, failure);
}

/// <summary>
/// Composes a ticket's chronological history timeline (CRM-139) from rows this
/// module already owns — the ticket itself plus the three append-only history
/// tables written by CRM-136/137/138 — rather than from a second canonical
/// history table. Same approach as <c>CustomerTimelineService</c> (CRM-129),
/// and the reason each of those tables already carries a
/// <c>(TicketId, timestamp)</c> index.
/// <para>
/// Duplicating every mutation into a parallel <c>ticket_history</c> table would
/// create a second source of truth for the same fact; projecting instead keeps
/// history append-oriented and auditable (BR) and lets a new capability
/// (SLA CRM-150, automation CRM-152/154, internal notes CRM-147) contribute by
/// adding its own rows plus one arm here — existing entries are never rewritten
/// or migrated (AC).
/// </para>
/// <para>
/// Merging and paging happen in memory: every source is scoped to a single
/// ticket id and read <c>AsNoTracking</c>, so the working set is bounded by one
/// ticket's own activity.
/// </para>
/// </summary>
internal sealed class TicketTimelineService(TicketManagementDbContext dbContext)
{
    public async Task<TicketTimelineResult> GetAsync(
        Guid ticketId,
        PaginationRequest pagination,
        TicketTimelineAudience audience,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketTimelineResult.Failed(TicketTimelineFailure.TicketNotFound);
        }

        List<TicketTimelineEntryResponse> entries =
        [
            // The creating actor is not recorded on the ticket: CRM-133 persists
            // no creator column, and the Audit module — which does record it —
            // belongs to another module and is not read across that boundary
            // here. A null ActorId reports "not attributable from this module"
            // rather than inventing an actor.
            new TicketTimelineEntryResponse(
                ticket.Id,
                "TicketCreated",
                ticket.CreatedAtUtc,
                0,
                TicketTimelineActorType.User,
                null,
                $"Ticket {ticket.TicketNumber} created with status {ticket.Status} via {ticket.Channel}",
                null,
                TicketTimelineVisibility.Customer),
        ];

        List<TicketAssignmentHistory> assignments = await dbContext.TicketAssignmentHistory
            .AsNoTracking()
            .Where(history => history.TicketId == ticketId)
            .ToListAsync(cancellationToken);
        entries.AddRange(assignments.Select(history => new TicketTimelineEntryResponse(
            history.Id,
            history.PreviousAgentId is null ? "TicketAssigned" : "TicketReassigned",
            history.ChangedAtUtc,
            0,
            ActorTypeOf(history.Source),
            history.ChangedBy,
            history.PreviousAgentId is null
                ? $"Assigned to agent {history.NewAgentId}"
                : $"Reassigned from agent {history.PreviousAgentId} to agent {history.NewAgentId}",
            history.Reason,
            // Who works a ticket internally is routing information, not customer
            // business.
            TicketTimelineVisibility.Internal)));

        List<TicketStatusHistory> statusChanges = await dbContext.TicketStatusHistory
            .AsNoTracking()
            .Where(history => history.TicketId == ticketId)
            .ToListAsync(cancellationToken);
        entries.AddRange(statusChanges.Select(history => new TicketTimelineEntryResponse(
            history.Id,
            "TicketStatusChanged",
            history.ChangedAtUtc,
            0,
            TicketTimelineActorType.User,
            history.ChangedBy,
            $"Status changed from {history.PreviousStatus} to {history.NewStatus}",
            history.Reason,
            // A customer may see THAT their request moved; the free-text reason
            // is dropped for that audience below, never shown here by accident.
            TicketTimelineVisibility.Customer)));

        List<TicketEscalationHistory> escalations = await dbContext.TicketEscalationHistory
            .AsNoTracking()
            .Where(history => history.TicketId == ticketId)
            .ToListAsync(cancellationToken);
        entries.AddRange(escalations.Select(history => new TicketTimelineEntryResponse(
            history.Id,
            "TicketEscalated",
            history.EscalatedAtUtc,
            0,
            ActorTypeOf(history.Source),
            history.EscalatedBy,
            $"Escalated from level {history.PreviousLevel} to level {history.NewLevel}"
                + $" ({history.TargetType} {history.TargetId})",
            history.Reason,
            // Internal routing, same reasoning as assignment.
            TicketTimelineVisibility.Internal)));

        // Audience filtering happens BEFORE sequencing and paging: a customer's
        // page numbering must match what that audience can actually see, and an
        // internal entry must not occupy a slot in their timeline.
        IEnumerable<TicketTimelineEntryResponse> visible = audience == TicketTimelineAudience.Customer
            ? entries
                .Where(entry => entry.Visibility == TicketTimelineVisibility.Customer)
                .Select(entry => entry with { Reason = null })
            : entries;

        // EventId is a total, stable tie-breaker, so two entries sharing a
        // timestamp keep the same relative order on every request — pages never
        // overlap or skip an entry (AC "stable pagination/order").
        List<TicketTimelineEntryResponse> ordered =
        [
            .. visible
                .OrderBy(entry => entry.OccurredAtUtc)
                .ThenBy(entry => entry.EventId)
                .Select((entry, index) => entry with { Sequence = index + 1 }),
        ];

        List<TicketTimelineEntryResponse> items =
        [
            .. ordered
                .Skip((pagination.Page - 1) * pagination.PageSize)
                .Take(pagination.PageSize),
        ];

        return TicketTimelineResult.Success(new PagedResult<TicketTimelineEntryResponse>(
            items, pagination.Page, pagination.PageSize, ordered.Count));
    }

    /// <summary>
    /// Automation-written history identifies its source distinctly from a human
    /// actor (BR). CRM-136/138 already carry the source on every row, so this
    /// stays a mapping rather than a guess.
    /// </summary>
    private static TicketTimelineActorType ActorTypeOf(TicketAssignmentSource source) =>
        source == TicketAssignmentSource.Automation
            ? TicketTimelineActorType.Automation
            : TicketTimelineActorType.User;

    private static TicketTimelineActorType ActorTypeOf(TicketEscalationSource source) =>
        source == TicketEscalationSource.Automation
            ? TicketTimelineActorType.Automation
            : TicketTimelineActorType.User;
}
