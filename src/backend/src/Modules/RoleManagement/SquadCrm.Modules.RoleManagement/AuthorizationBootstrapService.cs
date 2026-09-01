using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Modules.RoleManagement;

public enum AuthorizationBootstrapFailure
{
    None,
    SubjectNotFound,
    SubjectInactive,
    RoleNotFound,
    RoleInactive,
}

public readonly record struct AuthorizationBootstrapResult(AuthorizationBootstrapFailure Failure)
{
    public bool Succeeded => Failure == AuthorizationBootstrapFailure.None;
}

public sealed class AuthorizationBootstrapService(
    RoleManagementDbContext dbContext,
    IStaffSubjectReferenceReader subjectReader,
    IAuditRecorder auditRecorder)
{
    public async Task<AuthorizationBootstrapResult> BootstrapAsync(
        string subjectEmail,
        string roleCode,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = subjectEmail.Trim().ToUpperInvariant();
        StaffSubjectReference? subject = await subjectReader.FindByNormalizedEmailAsync(
            normalizedEmail, cancellationToken);
        if (subject is null)
        {
            return new(AuthorizationBootstrapFailure.SubjectNotFound);
        }
        if (!subject.IsActive)
        {
            return new(AuthorizationBootstrapFailure.SubjectInactive);
        }

        string normalizedRoleCode = RoleService.Normalize(roleCode);
        Role? role = await dbContext.Roles.SingleOrDefaultAsync(
            item => item.NormalizedCode == normalizedRoleCode, cancellationToken);
        if (role is null)
        {
            return new(AuthorizationBootstrapFailure.RoleNotFound);
        }
        if (!role.IsActive)
        {
            return new(AuthorizationBootstrapFailure.RoleInactive);
        }

        bool roleAssigned = !await dbContext.StaffSubjectRoles.AnyAsync(
            item => item.StaffSubjectId == subject.Id && item.RoleId == role.Id, cancellationToken);
        if (roleAssigned)
        {
            dbContext.StaffSubjectRoles.Add(new StaffSubjectRole
            {
                StaffSubjectId = subject.Id,
                RoleId = role.Id,
            });
        }

        string[] existing = await dbContext.RolePermissions
            .Where(item => item.RoleId == role.Id && Permissions.Bootstrap.Contains(item.PermissionCode))
            .Select(item => item.PermissionCode)
            .ToArrayAsync(cancellationToken);
        string[] missingPermissions = Permissions.Bootstrap.Except(existing, StringComparer.Ordinal).ToArray();
        foreach (string code in missingPermissions)
        {
            dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
        }

        if (missingPermissions.Length > 0)
        {
            dbContext.PermissionChangeAuditEvents.Add(new PermissionChangeAuditEvent
            {
                RoleId = role.Id,
                EventType = "bootstrap_permissions_granted",
                PermissionCodes = string.Join(',', missingPermissions.OrderBy(code => code, StringComparer.Ordinal)),
                ChangedByHandle = null,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (roleAssigned)
        {
            await auditRecorder.RecordAsync(
                new AuditRecordRequest(
                    "bootstrap-tool",
                    "role_assigned",
                    "StaffSubjectRole",
                    $"{subject.Id}:{role.Id}",
                    new Dictionary<string, string> { ["roleCode"] = role.Code }),
                cancellationToken);
        }

        return new(AuthorizationBootstrapFailure.None);
    }
}
