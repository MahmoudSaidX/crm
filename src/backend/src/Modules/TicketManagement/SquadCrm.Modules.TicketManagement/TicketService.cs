using Microsoft.EntityFrameworkCore;
using Npgsql;
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
