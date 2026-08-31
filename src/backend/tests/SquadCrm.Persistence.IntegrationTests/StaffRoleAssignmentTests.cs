using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class StaffRoleAssignmentTests
{
    public StaffRoleAssignmentTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Replace_AssignsAndReplacesRoles_Audits_AndRejectsUnknownInputs()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        Role roleA = await CreateRoleAsync(context);
        Role roleB = await CreateRoleAsync(context);
        Guid subjectId = Guid.NewGuid();
        StaffRoleAssignmentService service = new(
            context, new StubSubjectReader(new(subjectId, true)), new StubCurrentUserAccessor(subjectId.ToString()));

        StaffRoleAssignmentResult granted = await service.ReplaceAsync(subjectId, [roleA.Id, roleB.Id], CancellationToken.None);
        IReadOnlyList<RoleSummary> assigned = await service.GetAssignedRolesAsync(subjectId, CancellationToken.None);
        StaffRoleAssignmentResult replaced = await service.ReplaceAsync(subjectId, [roleA.Id], CancellationToken.None);
        IReadOnlyList<RoleSummary> afterReplace = await service.GetAssignedRolesAsync(subjectId, CancellationToken.None);
        StaffRoleAssignmentResult unknownRole = await service.ReplaceAsync(subjectId, [Guid.NewGuid()], CancellationToken.None);
        StaffRoleAssignmentResult unknownSubject = await new StaffRoleAssignmentService(
                context, new StubSubjectReader(null), new StubCurrentUserAccessor("operator"))
            .ReplaceAsync(Guid.NewGuid(), [roleA.Id], CancellationToken.None);

        Assert.Equal(StaffRoleAssignmentFailure.None, granted.Failure);
        Assert.Equal(2, assigned.Count);
        Assert.Equal(StaffRoleAssignmentFailure.None, replaced.Failure);
        Assert.Single(afterReplace);
        Assert.Equal(StaffRoleAssignmentFailure.InvalidRoleIds, unknownRole.Failure);
        Assert.Equal(StaffRoleAssignmentFailure.SubjectNotFound, unknownSubject.Failure);
        Assert.Equal(2, await context.StaffRoleAssignmentAuditEvents
            .CountAsync(item => item.StaffSubjectId == subjectId));
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
