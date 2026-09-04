using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class PermissionManagementTests
{
    public PermissionManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Catalog_IsSeeded_WithStableRoleCapabilities()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        PermissionService service = new(context, new StubCurrentUserAccessor("operator"));

        IReadOnlyList<PermissionResponse> catalog = await service.GetCatalogAsync(null, CancellationToken.None);

        Assert.Contains(catalog, item => item.Code == Permissions.RolesView && item.Module == "Role Management");
        Assert.Contains(catalog, item => item.Code == Permissions.RolesManage && item.Module == "Role Management");
    }

    [Fact]
    public async Task Replace_AddsAndRemovesGrants_Audits_AndTakesEffectImmediately()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role role = await CreateRoleAsync(context);
        Guid subjectId = Guid.NewGuid();
        context.StaffSubjectRoles.Add(new StaffSubjectRole { StaffSubjectId = subjectId, RoleId = role.Id });
        await context.SaveChangesAsync();
        PermissionService service = new(context, new StubCurrentUserAccessor(subjectId.ToString()));

        ReplacePermissionsResult granted = await service.ReplaceAsync(
            role.Id, [Permissions.RolesView, Permissions.RolesManage], CancellationToken.None);
        IReadOnlyList<string> current = await service.GetCurrentPermissionsAsync(subjectId, CancellationToken.None);
        ReplacePermissionsResult revoked = await service.ReplaceAsync(role.Id, [], CancellationToken.None);
        IReadOnlyList<string> afterRevocation = await service.GetCurrentPermissionsAsync(subjectId, CancellationToken.None);

        Assert.Equal(ReplacePermissionsFailure.None, granted.Failure);
        Assert.Equal([Permissions.RolesManage, Permissions.RolesView], current);
        Assert.Equal(ReplacePermissionsFailure.None, revoked.Failure);
        Assert.Empty(afterRevocation);
        Assert.Equal(2, await context.PermissionChangeAuditEvents.CountAsync(item => item.RoleId == role.Id));
        Assert.All(
            await context.PermissionChangeAuditEvents.Where(item => item.RoleId == role.Id).ToListAsync(),
            item => Assert.Equal(subjectId.ToString(), item.ChangedByHandle));
    }

    [Fact]
    public async Task InvalidOrDuplicateCodes_DoNotChangeGrantsOrWriteAudit()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role role = await CreateRoleAsync(context);
        PermissionService service = new(context, new StubCurrentUserAccessor("operator"));

        ReplacePermissionsResult unknown = await service.ReplaceAsync(role.Id, ["unknown.permission"], CancellationToken.None);
        ReplacePermissionsResult duplicate = await service.ReplaceAsync(
            role.Id, [Permissions.RolesView, Permissions.RolesView], CancellationToken.None);

        Assert.Equal(ReplacePermissionsFailure.InvalidPermissionCodes, unknown.Failure);
        Assert.Equal(ReplacePermissionsFailure.InvalidPermissionCodes, duplicate.Failure);
        Assert.Empty(await context.RolePermissions.Where(item => item.RoleId == role.Id).ToListAsync());
        Assert.Empty(await context.PermissionChangeAuditEvents.Where(item => item.RoleId == role.Id).ToListAsync());
    }

    [Fact]
    public async Task InactiveRole_StopsAuthorizationAndRejectsPermissionChanges()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role role = await CreateRoleAsync(context);
        Guid subjectId = Guid.NewGuid();
        context.StaffSubjectRoles.Add(new StaffSubjectRole { StaffSubjectId = subjectId, RoleId = role.Id });
        context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = Permissions.RolesView });
        await context.SaveChangesAsync();
        role.IsActive = false;
        await context.SaveChangesAsync();
        PermissionService service = new(context, new StubCurrentUserAccessor("operator"));

        Assert.Empty(await service.GetCurrentPermissionsAsync(subjectId, CancellationToken.None));
        Assert.Equal(
            ReplacePermissionsFailure.RoleInactive,
            (await service.ReplaceAsync(role.Id, [Permissions.RolesView], CancellationToken.None)).Failure);
    }

    [Fact]
    public async Task Bootstrap_IsIdempotent_AndRejectsInvalidOrInactiveSubjectsWithoutWrites()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role role = await CreateRoleAsync(context);
        Guid subjectId = Guid.NewGuid();
        AuthorizationBootstrapService valid = new(context, new StubSubjectReader(new(subjectId, true)), new NoOpAuditRecorder());

        Assert.True((await valid.BootstrapAsync("agent@example.test", role.Code, null, CancellationToken.None)).Succeeded);
        Assert.True((await valid.BootstrapAsync("agent@example.test", role.Code, null, CancellationToken.None)).Succeeded);
        Assert.Single(await context.StaffSubjectRoles.Where(item => item.StaffSubjectId == subjectId).ToListAsync());
        Assert.Equal(await context.PermissionDefinitions.CountAsync(), await context.RolePermissions.CountAsync(item => item.RoleId == role.Id));
        Assert.Single(await context.PermissionChangeAuditEvents.Where(
            item => item.RoleId == role.Id && item.EventType == "bootstrap_permissions_granted").ToListAsync());

        AuthorizationBootstrapService missing = new(context, new StubSubjectReader(null), new NoOpAuditRecorder());
        AuthorizationBootstrapService inactive = new(
            context, new StubSubjectReader(new(Guid.NewGuid(), false)), new NoOpAuditRecorder());
        Assert.Equal(AuthorizationBootstrapFailure.SubjectNotFound,
            (await missing.BootstrapAsync("missing@example.test", role.Code, null, CancellationToken.None)).Failure);
        Assert.Equal(AuthorizationBootstrapFailure.SubjectInactive,
            (await inactive.BootstrapAsync("inactive@example.test", role.Code, null, CancellationToken.None)).Failure);
        Assert.Single(await context.StaffSubjectRoles.Where(item => item.RoleId == role.Id).ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_CreatesMissingRole_GrantsFullCatalog_AndIsIdempotent()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Guid subjectId = Guid.NewGuid();
        AuthorizationBootstrapService service = new(context, new StubSubjectReader(new(subjectId, true)), new NoOpAuditRecorder());

        AuthorizationBootstrapResult result = await service.BootstrapAsync(
            "agent@example.test", "FIRST_ADMIN", "First Administrator", CancellationToken.None);
        Assert.True(result.Succeeded);

        Role role = await context.Roles.SingleAsync(item => item.NormalizedCode == "FIRST_ADMIN");
        Assert.True(role.IsActive);
        int catalogCount = await context.PermissionDefinitions.CountAsync();
        int grantCount = await context.RolePermissions.CountAsync(item => item.RoleId == role.Id);
        Assert.Equal(catalogCount, grantCount);
        Assert.Single(await context.RoleAuditEvents.Where(
            item => item.RoleId == role.Id && item.EventType == "created").ToListAsync());

        AuthorizationBootstrapResult secondResult = await service.BootstrapAsync(
            "agent@example.test", "FIRST_ADMIN", "First Administrator", CancellationToken.None);
        Assert.True(secondResult.Succeeded);

        Assert.Equal(1, await context.Roles.CountAsync(item => item.NormalizedCode == "FIRST_ADMIN"));
        Assert.Equal(grantCount, await context.RolePermissions.CountAsync(item => item.RoleId == role.Id));
        Assert.Single(await context.RoleAuditEvents.Where(
            item => item.RoleId == role.Id && item.EventType == "created").ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_ExistingInactiveRole_FailsWithoutWrites()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role role = await CreateRoleAsync(context);
        role.IsActive = false;
        await context.SaveChangesAsync();
        Guid subjectId = Guid.NewGuid();
        AuthorizationBootstrapService service = new(context, new StubSubjectReader(new(subjectId, true)), new NoOpAuditRecorder());

        AuthorizationBootstrapResult result = await service.BootstrapAsync(
            "agent@example.test", role.Code, null, CancellationToken.None);

        Assert.Equal(AuthorizationBootstrapFailure.RoleInactive, result.Failure);
        Assert.Empty(await context.RolePermissions.Where(item => item.RoleId == role.Id).ToListAsync());
        Assert.Empty(await context.StaffSubjectRoles.Where(item => item.RoleId == role.Id).ToListAsync());
    }

    private static async Task<Role> CreateRoleAsync(RoleManagementDbContext context)
    {
        RoleService service = new(context, new StubCurrentUserAccessor("operator"));
        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest($"Role {Guid.NewGuid():N}", $"ROLE_{Guid.NewGuid():N}"[..20], null),
            CancellationToken.None);
        return result.Role!;
    }

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => handle is not null;
        public string? Handle => handle;
    }

    private sealed class NoOpAuditRecorder : IAuditRecorder
    {
        public Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubSubjectReader(StaffSubjectReference? subject) : IStaffSubjectReferenceReader
    {
        public Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) => Task.FromResult(subject);

        public Task<StaffSubjectReference?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult(subject);
    }
}
