using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Modules.RoleManagement;

internal enum ReplacePermissionsFailure
{
    None,
    RoleNotFound,
    RoleInactive,
    InvalidPermissionCodes,
}

internal readonly record struct ReplacePermissionsResult(ReplacePermissionsFailure Failure)
{
    public static ReplacePermissionsResult Success() => new(ReplacePermissionsFailure.None);
    public static ReplacePermissionsResult Failed(ReplacePermissionsFailure failure) => new(failure);
}

internal sealed class PermissionService(
    RoleManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor)
{
    public async Task<IReadOnlyList<PermissionResponse>> GetCatalogAsync(
        Guid? roleId,
        CancellationToken cancellationToken)
    {
        HashSet<string> granted = roleId is null
            ? []
            : (await dbContext.RolePermissions.AsNoTracking()
                .Where(item => item.RoleId == roleId)
                .Select(item => item.PermissionCode)
                .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        return await dbContext.PermissionDefinitions.AsNoTracking()
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Code)
            .Select(permission => new PermissionResponse(
                permission.Code,
                permission.Name,
                permission.Module,
                permission.Description,
                granted.Contains(permission.Code)))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().AnyAsync(role => role.Id == roleId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetCurrentPermissionsAsync(
        Guid staffSubjectId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffSubjectRoles.AsNoTracking()
            .Where(assignment => assignment.StaffSubjectId == staffSubjectId && assignment.Role.IsActive)
            .Join(
                dbContext.RolePermissions,
                assignment => assignment.RoleId,
                permission => permission.RoleId,
                (_, permission) => permission.PermissionCode)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

    public async Task<ReplacePermissionsResult> ReplaceAsync(
        Guid roleId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        Role? role = await dbContext.Roles.SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken);
        if (role is null)
        {
            return ReplacePermissionsResult.Failed(ReplacePermissionsFailure.RoleNotFound);
        }
        if (!role.IsActive)
        {
            return ReplacePermissionsResult.Failed(ReplacePermissionsFailure.RoleInactive);
        }

        string[] requested = permissionCodes.Select(code => code?.Trim() ?? string.Empty).ToArray();
        if (requested.Any(string.IsNullOrWhiteSpace)
            || requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            return ReplacePermissionsResult.Failed(ReplacePermissionsFailure.InvalidPermissionCodes);
        }

        int knownCount = await dbContext.PermissionDefinitions.CountAsync(
            permission => requested.Contains(permission.Code), cancellationToken);
        if (knownCount != requested.Length)
        {
            return ReplacePermissionsResult.Failed(ReplacePermissionsFailure.InvalidPermissionCodes);
        }

        List<RolePermission> existing = await dbContext.RolePermissions
            .Where(item => item.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existing);
        dbContext.RolePermissions.AddRange(requested.Select(code => new RolePermission
        {
            RoleId = roleId,
            PermissionCode = code,
        }));
        dbContext.PermissionChangeAuditEvents.Add(new PermissionChangeAuditEvent
        {
            RoleId = roleId,
            EventType = "permissions_replaced",
            PermissionCodes = string.Join(',', requested.OrderBy(code => code, StringComparer.Ordinal)),
            ChangedByHandle = currentUserAccessor.Handle,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReplacePermissionsResult.Success();
    }
}
