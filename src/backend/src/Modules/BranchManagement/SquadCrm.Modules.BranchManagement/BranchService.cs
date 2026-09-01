using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Persistence;

namespace SquadCrm.Modules.BranchManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="Branch"/>.</summary>
public enum BranchMutationFailure
{
    None,
    DuplicateCode,
    NotFound,
}

public readonly record struct BranchMutationResult(Branch? Branch, BranchMutationFailure Failure)
{
    public static BranchMutationResult Success(Branch branch) => new(branch, BranchMutationFailure.None);
    public static BranchMutationResult Failed(BranchMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create/update
/// race (concurrent duplicate insert) into the same duplicate result the
/// pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class BranchService(
    BranchManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<BranchMutationResult> CreateAsync(
        CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, excludedId: null, cancellationToken))
        {
            return BranchMutationResult.Failed(BranchMutationFailure.DuplicateCode);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Branch branch = new()
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
        dbContext.Branches.Add(branch);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return BranchMutationResult.Failed(BranchMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(branch.Id, "created", cancellationToken);
        return BranchMutationResult.Success(branch);
    }

    public async Task<BranchMutationResult> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        CancellationToken cancellationToken)
    {
        Branch? branch = await dbContext.Branches.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (branch is null)
        {
            return BranchMutationResult.Failed(BranchMutationFailure.NotFound);
        }

        string normalizedCode = Normalize(request.Code);
        if (await CheckDuplicateAsync(normalizedCode, id, cancellationToken))
        {
            return BranchMutationResult.Failed(BranchMutationFailure.DuplicateCode);
        }

        branch.Code = request.Code.Trim();
        branch.NormalizedCode = normalizedCode;
        branch.ArabicName = request.ArabicName.Trim();
        branch.EnglishName = request.EnglishName.Trim();
        branch.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        branch.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return BranchMutationResult.Failed(BranchMutationFailure.DuplicateCode);
        }

        await RecordAuditAsync(branch.Id, "updated", cancellationToken);
        return BranchMutationResult.Success(branch);
    }

    public async Task<Branch?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Branches.AsNoTracking().SingleOrDefaultAsync(branch => branch.Id == id, cancellationToken);

    public async Task<PagedResult<Branch>> ListAsync(PaginationRequest pagination, CancellationToken cancellationToken)
    {
        IQueryable<Branch> query = dbContext.Branches.AsNoTracking().OrderBy(branch => branch.EnglishName);
        int totalCount = await query.CountAsync(cancellationToken);
        List<Branch> items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Branch>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<BranchMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public async Task<BranchMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<BranchMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string eventType,
        CancellationToken cancellationToken)
    {
        Branch? branch = await dbContext.Branches.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (branch is null)
        {
            return BranchMutationResult.Failed(BranchMutationFailure.NotFound);
        }

        // Never deletes the row — a deactivated branch remains readable/
        // listable for historical references, and only blocks new
        // memberships/routing/configuration (enforced by the consuming
        // modules, not built here).
        branch.IsActive = isActive;
        branch.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(branch.Id, eventType, cancellationToken);
        return BranchMutationResult.Success(branch);
    }

    private async Task<bool> CheckDuplicateAsync(string normalizedCode, Guid? excludedId, CancellationToken cancellationToken) =>
        await dbContext.Branches.AnyAsync(
            branch => branch.NormalizedCode == normalizedCode && (excludedId == null || branch.Id != excludedId),
            cancellationToken);

    private Task RecordAuditAsync(Guid branchId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "Branch", branchId.ToString(), Metadata: null),
            cancellationToken);

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
