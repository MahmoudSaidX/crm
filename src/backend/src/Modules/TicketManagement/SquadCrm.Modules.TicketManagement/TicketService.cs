using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Contracts;
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
    IBranchActiveLookup branchActiveLookup)
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
    public async Task<TicketDetailResponse?> GetDetailAsync(Guid id, CancellationToken cancellationToken) =>
        await (from ticket in dbContext.Tickets.AsNoTracking()
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
                   ticket.CreatedAtUtc,
                   ticket.UpdatedAtUtc,
                   ticket.Version))
            .SingleOrDefaultAsync(cancellationToken);

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
