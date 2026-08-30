using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Modules.RoleManagement;

/// <summary>Discriminates why a mutating call did not produce a <see cref="Role"/>.</summary>
public enum RoleMutationFailure
{
    None,
    DuplicateName,
    DuplicateCode,
    NotFound,
}

public readonly record struct RoleMutationResult(Role? Role, RoleMutationFailure Failure)
{
    public static RoleMutationResult Success(Role role) => new(role, RoleMutationFailure.None);
    public static RoleMutationResult Failed(RoleMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create/update
/// race (Edge Case: concurrent duplicate insert) into the same duplicate
/// result the pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class RoleService(RoleManagementDbContext dbContext, ICurrentUserAccessor currentUserAccessor)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<RoleMutationResult> CreateAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedName = Normalize(request.Name);
        string normalizedCode = Normalize(request.Code);

        RoleMutationFailure precheck = await CheckDuplicateAsync(normalizedName, normalizedCode, excludedId: null, cancellationToken);
        if (precheck != RoleMutationFailure.None)
        {
            return RoleMutationResult.Failed(precheck);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Role role = new()
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Code = request.Code.Trim(),
            NormalizedCode = normalizedCode,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.Roles.Add(role);
        RecordEvent(role.Id, "created");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            RoleMutationFailure raceFailure = await CheckDuplicateAsync(normalizedName, normalizedCode, excludedId: null, cancellationToken);
            return RoleMutationResult.Failed(
                raceFailure == RoleMutationFailure.None ? RoleMutationFailure.DuplicateName : raceFailure);
        }

        return RoleMutationResult.Success(role);
    }

    public async Task<RoleMutationResult> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        Role? role = await dbContext.Roles.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (role is null)
        {
            return RoleMutationResult.Failed(RoleMutationFailure.NotFound);
        }

        string normalizedName = Normalize(request.Name);
        string normalizedCode = Normalize(request.Code);
        RoleMutationFailure precheck = await CheckDuplicateAsync(normalizedName, normalizedCode, id, cancellationToken);
        if (precheck != RoleMutationFailure.None)
        {
            return RoleMutationResult.Failed(precheck);
        }

        role.Name = request.Name.Trim();
        role.NormalizedName = normalizedName;
        role.Code = request.Code.Trim();
        role.NormalizedCode = normalizedCode;
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.UpdatedAtUtc = DateTimeOffset.UtcNow;
        RecordEvent(role.Id, "updated");

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            RoleMutationFailure raceFailure = await CheckDuplicateAsync(normalizedName, normalizedCode, id, cancellationToken);
            return RoleMutationResult.Failed(
                raceFailure == RoleMutationFailure.None ? RoleMutationFailure.DuplicateName : raceFailure);
        }

        return RoleMutationResult.Success(role);
    }

    public async Task<Role?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public async Task<PagedResult<Role>> ListAsync(PaginationRequest pagination, CancellationToken cancellationToken)
    {
        IQueryable<Role> query = dbContext.Roles.AsNoTracking().OrderBy(role => role.Name);
        int totalCount = await query.CountAsync(cancellationToken);
        List<Role> items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Role>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<RoleMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public async Task<RoleMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        await SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<RoleMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string eventType,
        CancellationToken cancellationToken)
    {
        Role? role = await dbContext.Roles.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (role is null)
        {
            return RoleMutationResult.Failed(RoleMutationFailure.NotFound);
        }

        // Never deletes the row — a deactivated role remains readable/listable
        // (historical/user references, once CRM-111 exists, are preserved) and
        // simply blocks new assignment (enforced by CRM-111, not built here).
        role.IsActive = isActive;
        role.UpdatedAtUtc = DateTimeOffset.UtcNow;
        RecordEvent(role.Id, eventType);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RoleMutationResult.Success(role);
    }

    private async Task<RoleMutationFailure> CheckDuplicateAsync(
        string normalizedName,
        string normalizedCode,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Roles.AnyAsync(
                role => role.NormalizedName == normalizedName && (excludedId == null || role.Id != excludedId),
                cancellationToken))
        {
            return RoleMutationFailure.DuplicateName;
        }

        if (await dbContext.Roles.AnyAsync(
                role => role.NormalizedCode == normalizedCode && (excludedId == null || role.Id != excludedId),
                cancellationToken))
        {
            return RoleMutationFailure.DuplicateCode;
        }

        return RoleMutationFailure.None;
    }

    private void RecordEvent(Guid roleId, string eventType) =>
        dbContext.RoleAuditEvents.Add(new RoleAuditEvent
        {
            RoleId = roleId,
            EventType = eventType,
            ChangedByHandle = currentUserAccessor.Handle,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
