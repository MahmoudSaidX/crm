using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Modules.RoleManagement.DemoData;

/// <summary>
/// RoleManagement's own demo-data contributor: the four demo roles, their
/// permission grants and the demo staff assignments.
/// <para>
/// The permission catalog is read from the live <c>permission_definition</c>
/// table — there is no duplicated catalog here. Administrator receives every
/// registered code; the other roles are expressed as code <i>selectors</i> over
/// that same live catalog, and a selector that matches nothing (because the
/// catalog changed) is skipped rather than inserted, so a catalog change can
/// never produce a foreign-key failure or a silently stale grant list.
/// </para>
/// <para>
/// Grants are reconciled additively and nothing is ever revoked: the seeder must
/// coexist with a developer's own role edits.
/// </para>
/// </summary>
public sealed class RoleManagementDemoDataContributor : IDemoDataContributor
{
    private const string SeederHandle = "demo-data-seeder";

    /// <summary>
    /// Demo role composition. Administrator is special-cased to the full live
    /// catalog; every other role names the codes it needs.
    /// </summary>
    internal static readonly DemoRoleDefinition[] Roles =
    [
        new(
            "administrator",
            "Administrator",
            "DEMO DATA — full administrative access (every registered permission).",
            GrantsEveryRegisteredPermission: true,
            []),
        new(
            "support-manager",
            "Support Manager",
            "DEMO DATA — customer and ticket operations, without system administration.",
            GrantsEveryRegisteredPermission: false,
            [
                "customers.view",
                "customers.manage",
                "departments.view",
                "branches.view",
                "ticketcategories.view",
                "ticketpriorities.view",
                "tickets.view",
                "tickets.create",
                "tickets.assign",
                "tickets.changestatus",
                "tickets.escalate",
            ]),
        new(
            "support-agent",
            "Support Agent",
            "DEMO DATA — day-to-day support work on customers and tickets.",
            GrantsEveryRegisteredPermission: false,
            [
                // customers.manage is included because customer create/update,
                // contacts and notes all sit behind that single permission in the
                // current catalog — there is no finer-grained customer write
                // permission an agent could be given instead.
                "customers.view",
                "customers.manage",
                "ticketcategories.view",
                "ticketpriorities.view",
                "tickets.view",
                "tickets.create",
                "tickets.assign",
                "tickets.changestatus",
                "tickets.escalate",
            ]),
        new(
            "read-only",
            "Read Only",
            "DEMO DATA — read-only access. No mutation permission of any kind.",
            GrantsEveryRegisteredPermission: false,
            [
                "customers.view",
                "tickets.view",
                "ticketcategories.view",
                "ticketpriorities.view",
                "departments.view",
                "branches.view",
            ]),
    ];

    public string Name => "RoleManagement";

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using RoleManagementDbContext dbContext = new RoleManagementDbContextFactory().CreateDbContext([]);

        string[] catalog = await dbContext.PermissionDefinitions
            .Select(definition => definition.Code)
            .ToArrayAsync(cancellationToken);
        if (catalog.Length == 0)
        {
            throw new InvalidOperationException(
                "The permission catalog is empty. Run scripts/migrate before seeding demo data.");
        }

        int rolesCreated = 0;
        int grantsCreated = 0;
        int assignmentsCreated = 0;
        Dictionary<string, Guid> roleIdsByCode = [];

        foreach (DemoRoleDefinition definition in Roles)
        {
            string normalizedCode = RoleService.Normalize(definition.Code);
            Role? role = await dbContext.Roles.SingleOrDefaultAsync(
                candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);

            if (role is null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid(),
                    Name = definition.Name,
                    NormalizedName = RoleService.Normalize(definition.Name),
                    Code = definition.Code,
                    NormalizedCode = normalizedCode,
                    Description = definition.Description,
                    IsActive = true,
                    CreatedAtUtc = scope.NowUtc,
                    UpdatedAtUtc = scope.NowUtc,
                };
                dbContext.Roles.Add(role);
                dbContext.RoleAuditEvents.Add(new RoleAuditEvent
                {
                    RoleId = role.Id,
                    EventType = "created",
                    ChangedByHandle = SeederHandle,
                    OccurredAtUtc = scope.NowUtc,
                });
                rolesCreated++;
            }
            else if (!role.IsActive)
            {
                role.IsActive = true;
                role.UpdatedAtUtc = scope.NowUtc;
                dbContext.RoleAuditEvents.Add(new RoleAuditEvent
                {
                    RoleId = role.Id,
                    EventType = "activated",
                    ChangedByHandle = SeederHandle,
                    OccurredAtUtc = scope.NowUtc,
                });
            }

            roleIdsByCode[definition.Code] = role.Id;

            string[] desired = definition.GrantsEveryRegisteredPermission
                ? catalog
                : [.. catalog.Intersect(definition.PermissionCodes, StringComparer.Ordinal)];
            string[] existing = await dbContext.RolePermissions
                .Where(grant => grant.RoleId == role.Id)
                .Select(grant => grant.PermissionCode)
                .ToArrayAsync(cancellationToken);
            string[] missing = [.. desired.Except(existing, StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)];

            foreach (string code in missing)
            {
                dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
            }

            if (missing.Length > 0)
            {
                grantsCreated += missing.Length;
                dbContext.PermissionChangeAuditEvents.Add(new PermissionChangeAuditEvent
                {
                    RoleId = role.Id,
                    EventType = "demo_permissions_granted",
                    PermissionCodes = string.Join(',', missing),
                    ChangedByHandle = SeederHandle,
                    OccurredAtUtc = scope.NowUtc,
                });
            }
        }

        foreach (DemoStaffReference staff in scope.References.Staff)
        {
            if (!roleIdsByCode.TryGetValue(staff.RoleCode, out Guid roleId))
            {
                continue;
            }

            bool alreadyAssigned = await dbContext.StaffSubjectRoles.AnyAsync(
                assignment => assignment.StaffSubjectId == staff.Id && assignment.RoleId == roleId,
                cancellationToken);
            if (alreadyAssigned)
            {
                continue;
            }

            dbContext.StaffSubjectRoles.Add(new StaffSubjectRole
            {
                StaffSubjectId = staff.Id,
                RoleId = roleId,
            });
            dbContext.StaffRoleAssignmentAuditEvents.Add(new StaffRoleAssignmentAuditEvent
            {
                StaffSubjectId = staff.Id,
                EventType = "demo_role_assigned",
                RoleCodes = staff.RoleCode,
                ChangedByHandle = SeederHandle,
                OccurredAtUtc = scope.NowUtc,
            });
            assignmentsCreated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["roles created"] = rolesCreated,
                ["permission grants created"] = grantsCreated,
                ["role assignments created"] = assignmentsCreated,
                ["registered permissions"] = catalog.Length,
            });
    }
}

/// <summary>One demo role and the codes it should hold from the live catalog.</summary>
internal sealed record DemoRoleDefinition(
    string Code,
    string Name,
    string Description,
    bool GrantsEveryRegisteredPermission,
    string[] PermissionCodes);
