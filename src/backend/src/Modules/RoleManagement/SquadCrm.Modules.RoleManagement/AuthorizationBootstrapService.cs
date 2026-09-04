using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Modules.RoleManagement;

public enum AuthorizationBootstrapFailure
{
    None,
    SubjectNotFound,
    SubjectInactive,
    RoleInactive,
    RoleConflict,
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
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<AuthorizationBootstrapResult> BootstrapAsync(
        string subjectEmail,
        string roleCode,
        string? roleName,
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
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string effectiveName = string.IsNullOrWhiteSpace(roleName) ? roleCode : roleName;
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = effectiveName.Trim(),
                NormalizedName = RoleService.Normalize(effectiveName),
                Code = roleCode.Trim(),
                NormalizedCode = normalizedRoleCode,
                Description = "Bootstrapped by the RoleManagement.Bootstrap operator tool.",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            dbContext.Roles.Add(role);
            dbContext.RoleAuditEvents.Add(new RoleAuditEvent
            {
                RoleId = role.Id,
                EventType = "created",
                ChangedByHandle = null,
                OccurredAtUtc = now,
            });
        }
        else if (!role.IsActive)
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

        string[] catalogCodes = await dbContext.PermissionDefinitions
            .Select(item => item.Code)
            .ToArrayAsync(cancellationToken);
        string[] existingGrants = await dbContext.RolePermissions
            .Where(item => item.RoleId == role.Id)
            .Select(item => item.PermissionCode)
            .ToArrayAsync(cancellationToken);
        string[] missingPermissions = catalogCodes.Except(existingGrants, StringComparer.Ordinal).ToArray();
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return new(AuthorizationBootstrapFailure.RoleConflict);
        }

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

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
