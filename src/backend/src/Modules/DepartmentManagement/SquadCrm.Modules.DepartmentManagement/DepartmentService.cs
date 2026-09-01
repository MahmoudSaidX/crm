using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.DepartmentManagement.Persistence;

namespace SquadCrm.Modules.DepartmentManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="Department"/>.</summary>
public enum DepartmentMutationFailure
{
    None,
    DuplicateCode,
    NotFound,
}

public readonly record struct DepartmentMutationResult(Department? Department, DepartmentMutationFailure Failure)
{
    public static DepartmentMutationResult Success(Department department) => new(department, DepartmentMutationFailure.None);
    public static DepartmentMutationResult Failed(DepartmentMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create/update
/// race (concurrent duplicate insert) into the same duplicate result the
/// pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class DepartmentService(
    DepartmentManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<DepartmentMutationResult> CreateAsync(
        CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, excludedId: null, cancellationToken))
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.DuplicateCode);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Department department = new()
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            NormalizedCode = normalizedCode,
            ArabicName = request.ArabicName.Trim(),
            EnglishName = request.EnglishName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.Departments.Add(department);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(department.Id, "created", cancellationToken);
        return DepartmentMutationResult.Success(department);
    }

    public async Task<DepartmentMutationResult> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        Department? department = await dbContext.Departments.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (department is null)
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.NotFound);
        }

        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, id, cancellationToken))
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.DuplicateCode);
        }

        department.Code = request.Code.Trim();
        department.NormalizedCode = normalizedCode;
        department.ArabicName = request.ArabicName.Trim();
        department.EnglishName = request.EnglishName.Trim();
        department.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        department.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(department.Id, "updated", cancellationToken);
        return DepartmentMutationResult.Success(department);
    }

    public async Task<Department?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Departments.AsNoTracking().SingleOrDefaultAsync(department => department.Id == id, cancellationToken);

    public async Task<PagedResult<Department>> ListAsync(PaginationRequest pagination, CancellationToken cancellationToken)
    {
        IQueryable<Department> query = dbContext.Departments.AsNoTracking().OrderBy(department => department.EnglishName);
        int totalCount = await query.CountAsync(cancellationToken);
        List<Department> items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Department>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<DepartmentMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public async Task<DepartmentMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<DepartmentMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string eventType,
        CancellationToken cancellationToken)
    {
        Department? department = await dbContext.Departments.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (department is null)
        {
            return DepartmentMutationResult.Failed(DepartmentMutationFailure.NotFound);
        }

        // Never deletes the row — a deactivated department remains readable/
        // listable for historical references, and only blocks new
        // memberships/routing/configuration (enforced by the consuming
        // modules, not built here).
        department.IsActive = isActive;
        department.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(department.Id, eventType, cancellationToken);
        return DepartmentMutationResult.Success(department);
    }

    private async Task<bool> CheckDuplicateAsync(string normalizedCode, Guid? excludedId, CancellationToken cancellationToken) =>
        await dbContext.Departments.AnyAsync(
            department => department.NormalizedCode == normalizedCode && (excludedId == null || department.Id != excludedId),
            cancellationToken);

    private Task RecordAuditAsync(Guid departmentId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "Department", departmentId.ToString(), Metadata: null),
            cancellationToken);

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
