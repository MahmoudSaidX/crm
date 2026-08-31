using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Modules.RoleManagement;

internal sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;

internal sealed class PermissionAuthorizationHandler(RoleManagementDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        string? subject = context.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid staffSubjectId))
        {
            return;
        }

        bool granted = await dbContext.StaffSubjectRoles.AsNoTracking()
            .Where(assignment => assignment.StaffSubjectId == staffSubjectId && assignment.Role.IsActive)
            .Join(
                dbContext.RolePermissions,
                assignment => assignment.RoleId,
                permission => permission.RoleId,
                (_, permission) => permission.PermissionCode)
            .AnyAsync(code => code == requirement.Code);

        if (granted)
        {
            context.Succeed(requirement);
        }
    }
}
