using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SquadCrm.Modules.Audit;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class AuditTests
{
    public AuditTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task RecordAsync_Succeeds_AndPersistsOneAuditRecord()
    {
        await using AuditDbContext context = PostgresTestDatabase.CreateAuditContext();
        AuditRecorder recorder = new(context, NullLogger<AuditRecorder>.Instance);
        string entityId = Guid.NewGuid().ToString();

        await recorder.RecordAsync(
            new AuditRecordRequest("bootstrap-tool", "role_assigned", "StaffSubjectRole", entityId),
            CancellationToken.None);

        AuditRecord record = await context.AuditRecords.SingleAsync(item => item.EntityId == entityId);
        Assert.Equal("bootstrap-tool", record.ActorHandle);
        Assert.Equal("role_assigned", record.Action);
        Assert.Equal("StaffSubjectRole", record.EntityType);
        Assert.Null(record.MetadataJson);
    }

    [Fact]
    public async Task RecordAsync_WithMetadata_PersistsSerializedMetadataJson()
    {
        await using AuditDbContext context = PostgresTestDatabase.CreateAuditContext();
        AuditRecorder recorder = new(context, NullLogger<AuditRecorder>.Instance);
        string entityId = Guid.NewGuid().ToString();

        await recorder.RecordAsync(
            new AuditRecordRequest(
                "bootstrap-tool", "role_assigned", "StaffSubjectRole", entityId,
                new Dictionary<string, string> { ["roleCode"] = "ADMIN" }),
            CancellationToken.None);

        AuditRecord record = await context.AuditRecords.SingleAsync(item => item.EntityId == entityId);
        Assert.NotNull(record.MetadataJson);
        Assert.Contains("\"roleCode\":\"ADMIN\"", record.MetadataJson);
    }

    [Fact]
    public async Task Bootstrap_NewlyAssignedRole_ProducesExactlyOneAuditRecord()
    {
        await using RoleManagementDbContext roleManagementContext = PostgresTestDatabase.CreateRoleManagementContext();
        await using AuditDbContext auditContext = PostgresTestDatabase.CreateAuditContext();
        Role role = await CreateRoleAsync(roleManagementContext);
        Guid subjectId = Guid.NewGuid();

        AuthorizationBootstrapService service = new(
            roleManagementContext,
            new StubSubjectReader(new StaffSubjectReference(subjectId, true)),
            new AuditRecorder(auditContext, NullLogger<AuditRecorder>.Instance));

        AuthorizationBootstrapResult result = await service.BootstrapAsync(
            "agent@example.test", role.Code, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        List<AuditRecord> records = await auditContext.AuditRecords
            .Where(record => record.EntityType == "StaffSubjectRole"
                && record.EntityId == $"{subjectId}:{role.Id}")
            .ToListAsync();
        Assert.Single(records);
        Assert.Equal("role_assigned", records[0].Action);
        Assert.Equal("bootstrap-tool", records[0].ActorHandle);
        Assert.Contains("\"roleCode\":\"" + role.Code + "\"", records[0].MetadataJson);
    }

    [Fact]
    public async Task Bootstrap_SubjectAlreadyHasRole_ProducesNoAuditRecord()
    {
        await using RoleManagementDbContext roleManagementContext = PostgresTestDatabase.CreateRoleManagementContext();
        await using AuditDbContext auditContext = PostgresTestDatabase.CreateAuditContext();
        Role role = await CreateRoleAsync(roleManagementContext);
        Guid subjectId = Guid.NewGuid();
        StubSubjectReader subjectReader = new(new StaffSubjectReference(subjectId, true));

        AuthorizationBootstrapService first = new(
            roleManagementContext, subjectReader, new AuditRecorder(auditContext, NullLogger<AuditRecorder>.Instance));
        Assert.True((await first.BootstrapAsync("agent@example.test", role.Code, null, CancellationToken.None)).Succeeded);

        int countAfterFirst = await auditContext.AuditRecords.CountAsync(
            record => record.EntityId == $"{subjectId}:{role.Id}");
        Assert.Equal(1, countAfterFirst);

        // Second call: the subject already has the role, so the
        // `if (!AnyAsync(...))` branch in AuthorizationBootstrapService is
        // skipped and IAuditRecorder.RecordAsync must not be called again.
        AuthorizationBootstrapService second = new(
            roleManagementContext, subjectReader, new AuditRecorder(auditContext, NullLogger<AuditRecorder>.Instance));
        Assert.True((await second.BootstrapAsync("agent@example.test", role.Code, null, CancellationToken.None)).Succeeded);

        int countAfterSecond = await auditContext.AuditRecords.CountAsync(
            record => record.EntityId == $"{subjectId}:{role.Id}");
        Assert.Equal(1, countAfterSecond);
    }

    /// <summary>
    /// Regression test upholding the user's explicit decision (Story CRM-114,
    /// "Known Limitations &amp; Out of Scope"): <c>StaffUserService</c> and
    /// <c>PermissionService</c> keep their existing transactional
    /// <c>AuthenticationEvent</c>/<c>PermissionChangeAuditEvent</c> audit trail
    /// and must never gain a dependency on <see cref="IAuditRecorder"/> — no
    /// dual-write to both mechanisms for the same operation.
    /// </summary>
    [Theory]
    [InlineData(typeof(SquadCrm.Modules.StaffIdentity.StaffUserService))]
    [InlineData(typeof(PermissionService))]
    public void ExistingModuleLocalAuditServices_NeverDependOnIAuditRecorder(Type serviceType)
    {
        bool anyConstructorTakesAuditRecorder = serviceType
            .GetConstructors(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)
            .Any(constructor => constructor.GetParameters()
                .Any(parameter => parameter.ParameterType == typeof(IAuditRecorder)));

        Assert.False(
            anyConstructorTakesAuditRecorder,
            $"{serviceType.Name} must not depend on IAuditRecorder (dual-write was explicitly rejected).");
    }

    private static async Task<Role> CreateRoleAsync(RoleManagementDbContext context)
    {
        RoleService service = new(context, new StubCurrentUserAccessor("agent@example.test"));
        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest(
                $"Role {Guid.NewGuid():N}", $"ROLE_{Guid.NewGuid():N}"[..20], null),
            CancellationToken.None);
        return result.Role!;
    }

    private sealed class StubCurrentUserAccessor(string? handle) : SquadCrm.BuildingBlocks.Security.ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
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
