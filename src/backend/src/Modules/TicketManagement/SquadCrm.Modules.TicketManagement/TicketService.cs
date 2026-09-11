using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="Ticket"/>.</summary>
public enum TicketMutationFailure
{
    None,
    InvalidCustomer,
    InactiveCategory,
    InactivePriority,
    InactiveDepartment,
    InactiveBranch,
    DuplicateTicketNumber,
    TicketNotFound,

    /// <summary>Target agent is unknown or not an active staff user.</summary>
    IneligibleAgent,

    /// <summary>Reassignment away from an existing owner needs a reason.</summary>
    ReasonRequired,

    /// <summary>The caller's version is behind the stored ticket version.</summary>
    StaleVersion,

    /// <summary>Target status is not reachable from the ticket's current status.</summary>
    InvalidStatusTransition,

    /// <summary>A resolved or closed ticket has nothing left to escalate.</summary>
    TicketNotEscalatable,

    /// <summary>Escalation target is unknown or not active.</summary>
    InvalidEscalationTarget,
}

public readonly record struct TicketMutationResult(Ticket? Ticket, TicketMutationFailure Failure)
{
    public static TicketMutationResult Success(Ticket ticket) => new(ticket, TicketMutationFailure.None);
    public static TicketMutationResult Failed(TicketMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create race
/// (concurrent duplicate insert) into the same duplicate result path, rather
/// than letting a 500 leak through.
/// </summary>
internal sealed class TicketService(
    TicketManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    ICustomerExistsLookup customerExistsLookup,
    IDepartmentActiveLookup departmentActiveLookup,
    IBranchActiveLookup branchActiveLookup,
    IStaffSubjectReferenceReader staffSubjectReferenceReader)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<TicketMutationResult> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!await customerExistsLookup.ExistsAsync(request.CustomerId, cancellationToken))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InvalidCustomer);
        }

        // TicketCategory/TicketPriority live in this SAME module/DbContext —
        // queried directly, no cross-module contract needed (unlike
        // Department/Branch, which belong to other modules).
        bool categoryActive = await dbContext.TicketCategories.AsNoTracking()
            .AnyAsync(category => category.Id == request.CategoryId && category.IsActive, cancellationToken);
        if (!categoryActive)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InactiveCategory);
        }

        bool priorityActive = await dbContext.TicketPriorities.AsNoTracking()
            .AnyAsync(priority => priority.Id == request.PriorityId && priority.IsActive, cancellationToken);
        if (!priorityActive)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InactivePriority);
        }

        if (!await departmentActiveLookup.IsActiveAsync(request.DepartmentId, cancellationToken))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InactiveDepartment);
        }

        if (!await branchActiveLookup.IsActiveAsync(request.BranchId, cancellationToken))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InactiveBranch);
        }

        // SubcategoryId (if supplied) is stored as-is with no cross-validation
        // against CategoryId — no Subcategory catalog/entity exists in this
        // repo yet (see Ticket's type-level remarks). Not a silently invented
        // business rule: a documented scope gap until a subcategory story
        // exists.
        Ticket ticket = Ticket.Create(
            Guid.NewGuid(),
            GenerateTicketNumber(),
            request.CustomerId,
            request.Subject.Trim(),
            request.Description.Trim(),
            request.CategoryId,
            request.SubcategoryId,
            request.PriorityId,
            request.DepartmentId,
            request.BranchId,
            request.Channel,
            request.AssignedAgentId,
            DateTimeOffset.UtcNow);
        dbContext.Tickets.Add(ticket);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.DuplicateTicketNumber);
        }

        await RecordAuditAsync(ticket.Id, "created", cancellationToken);
        return TicketMutationResult.Success(ticket);
    }

    public async Task<PagedResult<Ticket>> ListAsync(
        TicketListQuery query,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        IQueryable<Ticket> filtered = dbContext.Tickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            filtered = filtered.Where(ticket =>
                ticket.TicketNumber.Contains(search) || ticket.Subject.Contains(search));
        }

        if (query.Statuses is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.Statuses.Contains(ticket.Status));
        }

        if (query.CategoryIds is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.CategoryIds.Contains(ticket.CategoryId));
        }

        if (query.PriorityIds is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.PriorityIds.Contains(ticket.PriorityId));
        }

        if (query.AssigneeIds is { Length: > 0 })
        {
            filtered = filtered.Where(ticket =>
                ticket.AssignedAgentId != null && query.AssigneeIds.Contains(ticket.AssignedAgentId.Value));
        }

        if (query.DepartmentIds is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.DepartmentIds.Contains(ticket.DepartmentId));
        }

        if (query.BranchIds is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.BranchIds.Contains(ticket.BranchId));
        }

        if (query.Channels is { Length: > 0 })
        {
            filtered = filtered.Where(ticket => query.Channels.Contains(ticket.Channel));
        }

        // Every branch orders by TicketNumber (unique) as a stable tiebreaker
        // after the requested sort key, so paginated results never reorder
        // across pages regardless of SortBy/SortDirection.
        IOrderedQueryable<Ticket> sorted = (query.SortBy, query.SortDirection) switch
        {
            (TicketSortBy.CreatedAtUtc, SortDirection.Desc) => filtered.OrderByDescending(t => t.CreatedAtUtc),
            (TicketSortBy.CreatedAtUtc, _) => filtered.OrderBy(t => t.CreatedAtUtc),
            (_, SortDirection.Desc) => filtered.OrderByDescending(t => t.TicketNumber),
            _ => filtered.OrderBy(t => t.TicketNumber),
        };
        IOrderedQueryable<Ticket> ordered = sorted.ThenBy(t => t.TicketNumber);

        int totalCount = await ordered.CountAsync(cancellationToken);
        List<Ticket> items = await ordered
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Ticket>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    /// <summary>
    /// Single-query ticket detail read (CRM-135). The category/priority joins
    /// intentionally do NOT filter on <c>IsActive</c>: a ticket created against
    /// a reference value that was later deactivated must still display that
    /// value's label (BR). Left joins, so a reference row deleted outright
    /// yields null names instead of hiding the ticket.
    /// </summary>
    public async Task<TicketDetailResponse?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        IQueryable<TicketDetailResponse> query =
            from ticket in dbContext.Tickets.AsNoTracking()
            where ticket.Id == id
            join category in dbContext.TicketCategories.AsNoTracking()
                on ticket.CategoryId equals category.Id into categories
            from category in categories.DefaultIfEmpty()
            join priority in dbContext.TicketPriorities.AsNoTracking()
                on ticket.PriorityId equals priority.Id into priorities
            from priority in priorities.DefaultIfEmpty()
            select new TicketDetailResponse(
                ticket.Id,
                ticket.TicketNumber,
                ticket.CustomerId,
                ticket.Subject,
                ticket.Description,
                ticket.CategoryId,
                category != null ? category.ArabicName : null,
                category != null ? category.EnglishName : null,
                category != null ? category.IsActive : (bool?)null,
                ticket.SubcategoryId,
                ticket.PriorityId,
                priority != null ? priority.ArabicName : null,
                priority != null ? priority.EnglishName : null,
                priority != null ? priority.IsActive : (bool?)null,
                priority != null ? priority.Rank : (int?)null,
                ticket.DepartmentId,
                ticket.BranchId,
                ticket.Status,
                ticket.Channel,
                ticket.AssignedAgentId,
                ticket.EscalationLevel,
                ticket.EscalationTargetType,
                ticket.EscalationTargetId,
                ticket.EscalatedAtUtc,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.Version,
                Array.Empty<string>());

        TicketDetailResponse? detail = await query.SingleOrDefaultAsync(cancellationToken);

        // Computed after materialization rather than inside the query: the
        // transition matrix is domain code, not something EF can translate to
        // SQL, and it is a UX hint only — ChangeStatusAsync re-validates it.
        return detail is null
            ? null
            : detail with
            {
                AllowedStatusTransitions = TicketStatusTransitions.AllowedFrom(detail.Status)
                    .Select(status => status.ToString())
                    .ToArray(),
            };
    }

    /// <summary>
    /// The canonical assignment capability (CRM-136). Manual assignment calls
    /// it today; the automatic-assignment stories (CRM-151/152) call the same
    /// method with <see cref="TicketAssignmentSource.Automation"/> rather than
    /// adding a second write path (BR).
    /// <para>
    /// Target eligibility is checked as "is an active staff user" only.
    /// <c>StaffUser.Branch</c>/<c>.Department</c> are free-text strings, not
    /// references to the Branch/Department modules, and no organizational-scope
    /// model exists on <c>ICurrentUserAccessor</c> yet — the story's Deadline
    /// Acceptance Override reduces eligibility to exactly this check rather
    /// than inventing a scope or capability model here.
    /// </para>
    /// </summary>
    public async Task<TicketMutationResult> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        TicketAssignmentSource source,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await dbContext.Tickets.SingleOrDefaultAsync(
            candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.TicketNotFound);
        }

        // Checked before anything else mutates: a caller working from a stale
        // read must not silently overwrite a newer owner (AC). The database
        // concurrency token below closes the remaining race window between
        // this check and SaveChanges.
        if (ticket.Version != request.Version)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        StaffSubjectReference? targetAgent =
            await staffSubjectReferenceReader.FindByIdAsync(request.TargetAgentId, cancellationToken);
        if (targetAgent is not { IsActive: true })
        {
            return TicketMutationResult.Failed(TicketMutationFailure.IneligibleAgent);
        }

        string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        // Required only when an existing owner is replaced — assigning an
        // unassigned ticket takes an optional reason (BR). Automation supplies
        // its own reason, so the same rule applies to it without a special case.
        bool replacesExistingOwner = ticket.AssignedAgentId is not null
            && ticket.AssignedAgentId != request.TargetAgentId;
        if (replacesExistingOwner && reason is null)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.ReasonRequired);
        }

        // Re-assigning to the current owner is accepted as a no-op: no version
        // bump, no history row and no event, so a double-submit cannot produce
        // a duplicate assignment record (BR).
        if (ticket.AssignedAgentId == request.TargetAgentId)
        {
            return TicketMutationResult.Success(ticket);
        }

        Guid? previousAgentId = ticket.AssignedAgentId;
        DateTimeOffset changedAtUtc = DateTimeOffset.UtcNow;
        ticket.Assign(request.TargetAgentId, reason, source, changedAtUtc);

        // Same change tracker, therefore the same transaction as the ticket
        // update and the outbox row: history, owner and event commit together
        // or not at all.
        dbContext.TicketAssignmentHistory.Add(new TicketAssignmentHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            PreviousAgentId = previousAgentId,
            NewAgentId = request.TargetAgentId,
            Reason = reason,
            Source = source,
            ChangedBy = currentUserAccessor.Handle ?? "unknown",
            ChangedAtUtc = changedAtUtc,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(ticket.Id, "assigned", cancellationToken);
        return TicketMutationResult.Success(ticket);
    }

    /// <summary>
    /// The canonical lifecycle transition capability (CRM-137). The manual
    /// endpoint calls it today; the escalation/automation stories
    /// (CRM-153/154) call the same method rather than adding a second write
    /// path.
    /// <para>
    /// Validity comes from <see cref="TicketStatusTransitions"/> — the same
    /// matrix the detail projection reports to the UI, so the hint and the
    /// enforced rule cannot drift apart. The UI gate is UX only; this check is
    /// authoritative.
    /// </para>
    /// </summary>
    public async Task<TicketMutationResult> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await dbContext.Tickets.SingleOrDefaultAsync(
            candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.TicketNotFound);
        }

        // Checked before anything else mutates: a caller working from a stale
        // read must not overwrite a newer status (AC). The database concurrency
        // token below closes the remaining race window before SaveChanges.
        if (ticket.Version != request.Version)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        // A transition to the ticket's current status is not in the matrix, so
        // a double-submit is rejected here rather than producing a second
        // history row and a duplicate event.
        if (!TicketStatusTransitions.IsAllowed(ticket.Status, request.TargetStatus))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InvalidStatusTransition);
        }

        string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is null && TicketStatusTransitions.RequiresReason(ticket.Status, request.TargetStatus))
        {
            return TicketMutationResult.Failed(TicketMutationFailure.ReasonRequired);
        }

        TicketStatus previousStatus = ticket.Status;
        DateTimeOffset changedAtUtc = DateTimeOffset.UtcNow;
        ticket.ChangeStatus(request.TargetStatus, reason, changedAtUtc);

        // Same change tracker, therefore the same transaction as the ticket
        // update and the outbox row: history, status and event commit together
        // or not at all. Reopening appends a row; earlier rows are untouched,
        // so a prior resolution/closure is preserved (BR).
        dbContext.TicketStatusHistory.Add(new TicketStatusHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            PreviousStatus = previousStatus,
            NewStatus = request.TargetStatus,
            Reason = reason,
            ChangedBy = currentUserAccessor.Handle ?? "unknown",
            ChangedAtUtc = changedAtUtc,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(ticket.Id, "status-changed", cancellationToken);
        return TicketMutationResult.Success(ticket);
    }

    /// <summary>
    /// The canonical escalation capability (CRM-138). The manual endpoint calls
    /// it today; the automatic-escalation stories (CRM-153/154) call the same
    /// method with <see cref="TicketEscalationSource.Automation"/> rather than
    /// adding a second write path (BR).
    /// <para>
    /// The new level is derived here as <c>current + 1</c> — never taken from
    /// the caller — so a client cannot corrupt the level sequence, and the
    /// version check plus the database concurrency token make two concurrent
    /// escalations resolve to one winner instead of two level bumps (AC).
    /// </para>
    /// <para>
    /// Escalation leaves <see cref="Ticket.Status"/> untouched: escalation is
    /// not a lifecycle status (BR).
    /// </para>
    /// </summary>
    public async Task<TicketMutationResult> EscalateAsync(
        Guid ticketId,
        EscalateTicketRequest request,
        TicketEscalationSource source,
        CancellationToken cancellationToken)
    {
        Ticket? ticket = await dbContext.Tickets.SingleOrDefaultAsync(
            candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.TicketNotFound);
        }

        // Checked before anything else mutates: a caller working from a stale
        // read must not stack a second escalation onto a newer one (AC). The
        // database concurrency token below closes the remaining race window
        // before SaveChanges.
        if (ticket.Version != request.Version)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        // Eligibility: a ticket that is already resolved or closed has nothing
        // left to escalate. Every other lifecycle status is eligible.
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.TicketNotEscalatable);
        }

        string reason = request.Reason.Trim();
        if (reason.Length == 0)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.ReasonRequired);
        }

        // Target validity, as strong as the existing cross-module contracts
        // allow: an active staff user or an active department. No
        // organizational-scope model exists on ICurrentUserAccessor yet (the
        // same documented gap as CRM-136), so no scope check is faked here.
        bool targetValid = request.TargetType switch
        {
            TicketEscalationTargetType.Agent =>
                await staffSubjectReferenceReader.FindByIdAsync(request.TargetId, cancellationToken)
                    is { IsActive: true },
            TicketEscalationTargetType.Department =>
                await departmentActiveLookup.IsActiveAsync(request.TargetId, cancellationToken),
            _ => false,
        };
        if (!targetValid)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.InvalidEscalationTarget);
        }

        DateTimeOffset escalatedAtUtc = DateTimeOffset.UtcNow;
        int previousLevel = ticket.EscalationLevel;
        int newLevel = previousLevel + 1;
        ticket.Escalate(newLevel, request.TargetType, request.TargetId, reason, source, escalatedAtUtc);

        // Same change tracker, therefore the same transaction as the ticket
        // update and the outbox row: history, escalation state and event commit
        // together or not at all.
        dbContext.TicketEscalationHistory.Add(new TicketEscalationHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            PreviousLevel = previousLevel,
            NewLevel = newLevel,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Reason = reason,
            Source = source,
            EscalatedBy = currentUserAccessor.Handle ?? "unknown",
            EscalatedAtUtc = escalatedAtUtc,
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TicketMutationResult.Failed(TicketMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(ticket.Id, "escalated", cancellationToken);
        return TicketMutationResult.Success(ticket);
    }

    private Task RecordAuditAsync(Guid ticketId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "Ticket", ticketId.ToString(), Metadata: null),
            cancellationToken);

    private static string GenerateTicketNumber() => $"TKT-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
