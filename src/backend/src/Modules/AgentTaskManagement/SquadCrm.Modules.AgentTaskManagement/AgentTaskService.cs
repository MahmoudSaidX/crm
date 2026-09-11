using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.AgentTaskManagement.Persistence;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.TicketManagement.Contracts;

namespace SquadCrm.Modules.AgentTaskManagement;

/// <summary>Discriminates why a mutating call did not produce an <see cref="AgentTask"/>.</summary>
public enum AgentTaskMutationFailure
{
    None,

    /// <summary>The caller's authenticated handle could not be resolved to a user id — fail-closed, never a guess.</summary>
    OwnerUnresolved,

    /// <summary>An explicitly supplied owner is unknown or not an active staff user.</summary>
    IneligibleOwner,

    InvalidTicket,
    InvalidCustomer,
    TaskNotFound,

    /// <summary>The caller is neither the task's owner nor (today) any permitted manager — no manager model exists yet (plan).</summary>
    NotOwner,

    /// <summary>The caller's version is behind the stored task version.</summary>
    StaleVersion,

    /// <summary>Completing a task that is already completed.</summary>
    AlreadyCompleted,

    /// <summary>Reopening a task that is not completed.</summary>
    NotCompleted,
}

public readonly record struct AgentTaskMutationResult(AgentTask? AgentTask, AgentTaskMutationFailure Failure)
{
    public static AgentTaskMutationResult Success(AgentTask agentTask) => new(agentTask, AgentTaskMutationFailure.None);
    public static AgentTaskMutationResult Failed(AgentTaskMutationFailure failure) => new(null, failure);
}

internal sealed class AgentTaskService(
    AgentTaskManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    ICustomerExistsLookup customerExistsLookup,
    ITicketExistsLookup ticketExistsLookup,
    IStaffSubjectReferenceReader staffSubjectReferenceReader)
{
    public async Task<AgentTaskMutationResult> CreateAsync(
        CreateAgentTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(currentUserAccessor.Handle, out Guid callerId))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.OwnerUnresolved);
        }

        Guid ownerUserId = callerId;
        if (request.OwnerUserId is { } requestedOwnerId && requestedOwnerId != callerId)
        {
            StaffSubjectReference? owner =
                await staffSubjectReferenceReader.FindByIdAsync(requestedOwnerId, cancellationToken);
            if (owner is not { IsActive: true })
            {
                return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.IneligibleOwner);
            }

            ownerUserId = requestedOwnerId;
        }

        if (request.TicketId is { } ticketId
            && !await ticketExistsLookup.ExistsAsync(ticketId, cancellationToken))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.InvalidTicket);
        }

        if (request.CustomerId is { } customerId
            && !await customerExistsLookup.ExistsAsync(customerId, cancellationToken))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.InvalidCustomer);
        }

        AgentTask task = AgentTask.Create(
            Guid.NewGuid(),
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            ownerUserId,
            request.TicketId,
            request.CustomerId,
            request.DueAtUtc,
            DateTimeOffset.UtcNow);
        dbContext.AgentTasks.Add(task);

        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(task.Id, "created", cancellationToken);
        return AgentTaskMutationResult.Success(task);
    }

    /// <summary>
    /// "My Tasks" (<see cref="AgentTaskListQuery.MyTasksOnly"/>) resolves the
    /// caller id server-side, same pattern as
    /// <c>TicketService.ListAsync</c>'s <c>assignedToMe</c> — fail-closed: an
    /// unparsable caller handle yields an empty page rather than the
    /// unfiltered list.
    /// </summary>
    public async Task<PagedResult<AgentTask>> ListAsync(
        AgentTaskListQuery query,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        IQueryable<AgentTask> filtered = dbContext.AgentTasks.AsNoTracking();

        if (query.MyTasksOnly)
        {
            if (!Guid.TryParse(currentUserAccessor.Handle, out Guid currentUserId))
            {
                return new PagedResult<AgentTask>([], pagination.Page, pagination.PageSize, 0);
            }

            filtered = filtered.Where(task => task.OwnerUserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            filtered = filtered.Where(task => task.Title.Contains(search));
        }

        if (query.Statuses is { Length: > 0 })
        {
            filtered = filtered.Where(task => query.Statuses.Contains(task.Status));
        }

        if (query.DueBefore is { } dueBefore)
        {
            filtered = filtered.Where(task => task.DueAtUtc != null && task.DueAtUtc <= dueBefore);
        }

        if (query.DueAfter is { } dueAfter)
        {
            filtered = filtered.Where(task => task.DueAtUtc != null && task.DueAtUtc >= dueAfter);
        }

        // A stable tiebreaker (Id) after the requested sort key, so paginated
        // results never reorder across pages regardless of SortBy/SortDirection.
        IOrderedQueryable<AgentTask> sorted = (query.SortBy, query.SortDirection) switch
        {
            (AgentTaskSortBy.CreatedAtUtc, SortDirection.Desc) => filtered.OrderByDescending(t => t.CreatedAtUtc),
            (AgentTaskSortBy.CreatedAtUtc, _) => filtered.OrderBy(t => t.CreatedAtUtc),
            // A task with no due date sorts after every due task regardless of
            // direction, rather than colliding at one end via a null default.
            (_, SortDirection.Desc) => filtered.OrderBy(t => t.DueAtUtc == null)
                .ThenByDescending(t => t.DueAtUtc),
            _ => filtered.OrderBy(t => t.DueAtUtc == null).ThenBy(t => t.DueAtUtc),
        };
        IOrderedQueryable<AgentTask> ordered = sorted.ThenBy(t => t.Id);

        int totalCount = await ordered.CountAsync(cancellationToken);
        List<AgentTask> items = await ordered
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AgentTask>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    /// <summary>
    /// Owner-gated read (AC "agent can... view... tasks they own or are
    /// permitted to manage" — no manager model exists yet, so only the owner
    /// today; plan).
    /// </summary>
    public async Task<AgentTaskMutationResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AgentTask? task = await dbContext.AgentTasks.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (task is null)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.TaskNotFound);
        }

        return IsOwner(task) ? AgentTaskMutationResult.Success(task) : AgentTaskMutationResult.Failed(AgentTaskMutationFailure.NotOwner);
    }

    public async Task<AgentTaskMutationResult> UpdateAsync(
        Guid id,
        UpdateAgentTaskRequest request,
        CancellationToken cancellationToken)
    {
        AgentTask? task = await dbContext.AgentTasks.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);
        if (task is null)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.TaskNotFound);
        }

        if (!IsOwner(task))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.NotOwner);
        }

        if (task.Version != request.Version)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        if (request.TicketId is { } ticketId
            && !await ticketExistsLookup.ExistsAsync(ticketId, cancellationToken))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.InvalidTicket);
        }

        if (request.CustomerId is { } customerId
            && !await customerExistsLookup.ExistsAsync(customerId, cancellationToken))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.InvalidCustomer);
        }

        task.ApplyEdit(
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            request.TicketId,
            request.CustomerId,
            request.DueAtUtc,
            DateTimeOffset.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(task.Id, "updated", cancellationToken);
        return AgentTaskMutationResult.Success(task);
    }

    /// <summary>
    /// AC "completing a task preserves history rather than deleting it" — the
    /// row is updated in place, never removed.
    /// </summary>
    public async Task<AgentTaskMutationResult> CompleteAsync(
        Guid id,
        AgentTaskVersionedActionRequest request,
        CancellationToken cancellationToken)
    {
        AgentTask? task = await dbContext.AgentTasks.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);
        if (task is null)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.TaskNotFound);
        }

        if (!IsOwner(task))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.NotOwner);
        }

        if (task.Version != request.Version)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        if (task.Status == AgentTaskStatus.Completed)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.AlreadyCompleted);
        }

        task.Complete(DateTimeOffset.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(task.Id, "completed", cancellationToken);
        return AgentTaskMutationResult.Success(task);
    }

    /// <summary>
    /// BR: completed tasks are immutable in completion metadata except through
    /// this explicitly supported reopen workflow.
    /// </summary>
    public async Task<AgentTaskMutationResult> ReopenAsync(
        Guid id,
        AgentTaskVersionedActionRequest request,
        CancellationToken cancellationToken)
    {
        AgentTask? task = await dbContext.AgentTasks.SingleOrDefaultAsync(
            candidate => candidate.Id == id, cancellationToken);
        if (task is null)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.TaskNotFound);
        }

        if (!IsOwner(task))
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.NotOwner);
        }

        if (task.Version != request.Version)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        if (task.Status != AgentTaskStatus.Completed)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.NotCompleted);
        }

        task.Reopen(DateTimeOffset.UtcNow);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AgentTaskMutationResult.Failed(AgentTaskMutationFailure.StaleVersion);
        }

        await RecordAuditAsync(task.Id, "reopened", cancellationToken);
        return AgentTaskMutationResult.Success(task);
    }

    /// <summary>
    /// No organizational-scope/manager model exists yet (plan) — ownership is
    /// the only access rule this story implements.
    /// </summary>
    private bool IsOwner(AgentTask task) =>
        Guid.TryParse(currentUserAccessor.Handle, out Guid currentUserId) && task.OwnerUserId == currentUserId;

    private Task RecordAuditAsync(Guid taskId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "AgentTask", taskId.ToString(), Metadata: null),
            cancellationToken);
}
