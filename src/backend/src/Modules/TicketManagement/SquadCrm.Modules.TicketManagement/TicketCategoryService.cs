using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="TicketCategory"/>.</summary>
public enum TicketCategoryMutationFailure
{
    None,
    DuplicateCode,
    InactiveDepartment,
    NotFound,
}

public readonly record struct TicketCategoryMutationResult(TicketCategory? TicketCategory, TicketCategoryMutationFailure Failure)
{
    public static TicketCategoryMutationResult Success(TicketCategory ticketCategory) => new(ticketCategory, TicketCategoryMutationFailure.None);
    public static TicketCategoryMutationResult Failed(TicketCategoryMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create/update
/// race (concurrent duplicate insert) into the same duplicate result the
/// pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class TicketCategoryService(
    TicketManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    IDepartmentActiveLookup departmentActiveLookup)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<TicketCategoryMutationResult> CreateAsync(
        CreateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DefaultDepartmentId is Guid departmentId
            && !await departmentActiveLookup.IsActiveAsync(departmentId, cancellationToken))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.InactiveDepartment);
        }

        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, excludedId: null, cancellationToken))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.DuplicateCode);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TicketCategory category = new()
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            NormalizedCode = normalizedCode,
            ArabicName = request.ArabicName.Trim(),
            EnglishName = request.EnglishName.Trim(),
            DefaultDepartmentId = request.DefaultDepartmentId,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.TicketCategories.Add(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(category.Id, "created", cancellationToken);
        return TicketCategoryMutationResult.Success(category);
    }

    public async Task<TicketCategoryMutationResult> UpdateAsync(
        Guid id,
        UpdateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        TicketCategory? category = await dbContext.TicketCategories.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (category is null)
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.NotFound);
        }

        if (request.DefaultDepartmentId is Guid departmentId
            && !await departmentActiveLookup.IsActiveAsync(departmentId, cancellationToken))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.InactiveDepartment);
        }

        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, id, cancellationToken))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.DuplicateCode);
        }

        category.Code = request.Code.Trim();
        category.NormalizedCode = normalizedCode;
        category.ArabicName = request.ArabicName.Trim();
        category.EnglishName = request.EnglishName.Trim();
        category.DefaultDepartmentId = request.DefaultDepartmentId;
        category.SortOrder = request.SortOrder;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(category.Id, "updated", cancellationToken);
        return TicketCategoryMutationResult.Success(category);
    }

    public async Task<TicketCategory?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.TicketCategories.AsNoTracking().SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<PagedResult<TicketCategory>> ListAsync(PaginationRequest pagination, CancellationToken cancellationToken)
    {
        IQueryable<TicketCategory> query = dbContext.TicketCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.EnglishName);
        int totalCount = await query.CountAsync(cancellationToken);
        List<TicketCategory> items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TicketCategory>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<TicketCategoryMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public async Task<TicketCategoryMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<TicketCategoryMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string eventType,
        CancellationToken cancellationToken)
    {
        TicketCategory? category = await dbContext.TicketCategories.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (category is null)
        {
            return TicketCategoryMutationResult.Failed(TicketCategoryMutationFailure.NotFound);
        }

        // Never deletes the row — a deactivated category remains readable/
        // listable for historical references, and only blocks new/changed
        // ticket selection (enforced by the consuming Ticket module, not
        // built here — there is no Ticket entity yet).
        category.IsActive = isActive;
        category.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(category.Id, eventType, cancellationToken);
        return TicketCategoryMutationResult.Success(category);
    }

    private async Task<bool> CheckDuplicateAsync(string normalizedCode, Guid? excludedId, CancellationToken cancellationToken) =>
        await dbContext.TicketCategories.AnyAsync(
            category => category.NormalizedCode == normalizedCode && (excludedId == null || category.Id != excludedId),
            cancellationToken);

    private Task RecordAuditAsync(Guid categoryId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "TicketCategory", categoryId.ToString(), Metadata: null),
            cancellationToken);

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
