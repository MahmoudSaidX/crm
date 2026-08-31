using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Modules.RoleManagement;

internal enum StaffRoleAssignmentFailure
{
    None,
    SubjectNotFound,
    InvalidRoleIds,
}

internal readonly record struct StaffRoleAssignmentResult(StaffRoleAssignmentFailure Failure)
{
    public static StaffRoleAssignmentResult Success() => new(StaffRoleAssignmentFailure.None);
    public static StaffRoleAssignmentResult Failed(StaffRoleAssignmentFailure failure) => new(failure);
}

internal sealed class StaffRoleAssignmentService(
    RoleManagementDbContext dbContext,
    IStaffSubjectReferenceReader subjectReader,
    ICurrentUserAccessor currentUserAccessor)
{
    public async Task<IReadOnlyList<RoleSummary>> GetAssignedRolesAsync(
        Guid staffSubjectId,
        CancellationToken cancellationToken) =>
        await dbContext.StaffSubjectRoles.AsNoTracking()
            .Where(assignment => assignment.StaffSubjectId == staffSubjectId)
            .OrderBy(assignment => assignment.Role.Name)
            .Select(assignment => new RoleSummary(assignment.Role.Id, assignment.Role.Name, assignment.Role.Code))
            .ToListAsync(cancellationToken);

    public async Task<StaffRoleAssignmentResult> ReplaceAsync(
        Guid staffSubjectId,
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        StaffSubjectReference? subject = await subjectReader.FindByIdAsync(staffSubjectId, cancellationToken);
        if (subject is null)
        {
            return StaffRoleAssignmentResult.Failed(StaffRoleAssignmentFailure.SubjectNotFound);
        }

        Guid[] requested = roleIds.Distinct().ToArray();
        if (requested.Length != roleIds.Count)
        {
            return StaffRoleAssignmentResult.Failed(StaffRoleAssignmentFailure.InvalidRoleIds);
        }

        List<Role> roles = await dbContext.Roles
            .Where(role => requested.Contains(role.Id))
            .ToListAsync(cancellationToken);
        if (roles.Count != requested.Length)
        {
            return StaffRoleAssignmentResult.Failed(StaffRoleAssignmentFailure.InvalidRoleIds);
        }

        List<StaffSubjectRole> existing = await dbContext.StaffSubjectRoles
            .Where(assignment => assignment.StaffSubjectId == staffSubjectId)
            .ToListAsync(cancellationToken);
        dbContext.StaffSubjectRoles.RemoveRange(existing);
        dbContext.StaffSubjectRoles.AddRange(requested.Select(roleId => new StaffSubjectRole
        {
            StaffSubjectId = staffSubjectId,
            RoleId = roleId,
        }));
        dbContext.StaffRoleAssignmentAuditEvents.Add(new StaffRoleAssignmentAuditEvent
        {
            StaffSubjectId = staffSubjectId,
            EventType = "roles_replaced",
            RoleCodes = string.Join(',', roles.Select(role => role.Code).OrderBy(code => code, StringComparer.Ordinal)),
            ChangedByHandle = currentUserAccessor.Handle,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return StaffRoleAssignmentResult.Success();
    }
}
