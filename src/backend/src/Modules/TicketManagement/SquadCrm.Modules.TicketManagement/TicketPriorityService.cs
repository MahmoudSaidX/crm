using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="TicketPriority"/>.</summary>
public enum TicketPriorityMutationFailure
{
    None,
    DuplicateCode,
    NotFound,
}

public readonly record struct TicketPriorityMutationResult(TicketPriority? TicketPriority, TicketPriorityMutationFailure Failure)
{
    public static TicketPriorityMutationResult Success(TicketPriority ticketPriority) => new(ticketPriority, TicketPriorityMutationFailure.None);
    public static TicketPriorityMutationResult Failed(TicketPriorityMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create/update
/// race (concurrent duplicate insert) into the same duplicate result the
/// pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class TicketPriorityService(
    TicketManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<TicketPriorityMutationResult> CreateAsync(
        CreateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, excludedId: null, cancellationToken))
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.DuplicateCode);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TicketPriority priority = new()
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            NormalizedCode = normalizedCode,
            ArabicName = request.ArabicName.Trim(),
            EnglishName = request.EnglishName.Trim(),
            Rank = request.Rank,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.TicketPriorities.Add(priority);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(priority.Id, "created", cancellationToken);
        return TicketPriorityMutationResult.Success(priority);
    }

    public async Task<TicketPriorityMutationResult> UpdateAsync(
        Guid id,
        UpdateTicketPriorityRequest request,
        CancellationToken cancellationToken)
    {
        TicketPriority? priority = await dbContext.TicketPriorities.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (priority is null)
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.NotFound);
        }

        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, id, cancellationToken))
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.DuplicateCode);
        }

        priority.Code = request.Code.Trim();
        priority.NormalizedCode = normalizedCode;
        priority.ArabicName = request.ArabicName.Trim();
        priority.EnglishName = request.EnglishName.Trim();
        priority.Rank = request.Rank;
        priority.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        priority.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(priority.Id, "updated", cancellationToken);
        return TicketPriorityMutationResult.Success(priority);
    }

    public async Task<TicketPriority?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.TicketPriorities.AsNoTracking().SingleOrDefaultAsync(priority => priority.Id == id, cancellationToken);

    public async Task<PagedResult<TicketPriority>> ListAsync(PaginationRequest pagination, CancellationToken cancellationToken)
    {
        IQueryable<TicketPriority> query = dbContext.TicketPriorities.AsNoTracking()
            .OrderBy(priority => priority.Rank)
            .ThenBy(priority => priority.EnglishName);
        int totalCount = await query.CountAsync(cancellationToken);
        List<TicketPriority> items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TicketPriority>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<TicketPriorityMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public async Task<TicketPriorityMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<TicketPriorityMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string eventType,
        CancellationToken cancellationToken)
    {
        TicketPriority? priority = await dbContext.TicketPriorities.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (priority is null)
        {
            return TicketPriorityMutationResult.Failed(TicketPriorityMutationFailure.NotFound);
        }

        // Never deletes the row — a deactivated priority remains readable/
        // listable for historical references, and only blocks new/changed
        // ticket selection (enforced by the consuming Ticket module, not
        // built here — there is no Ticket entity yet).
        priority.IsActive = isActive;
        priority.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(priority.Id, eventType, cancellationToken);
        return TicketPriorityMutationResult.Success(priority);
    }

    private async Task<bool> CheckDuplicateAsync(string normalizedCode, Guid? excludedId, CancellationToken cancellationToken) =>
        await dbContext.TicketPriorities.AnyAsync(
            priority => priority.NormalizedCode == normalizedCode && (excludedId == null || priority.Id != excludedId),
            cancellationToken);

    private Task RecordAuditAsync(Guid priorityId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "TicketPriority", priorityId.ToString(), Metadata: null),
            cancellationToken);

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
