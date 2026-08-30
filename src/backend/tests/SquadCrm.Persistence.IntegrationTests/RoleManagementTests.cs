using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class RoleManagementTests
{
    public RoleManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_AndProducesOneCreatedAuditEvent()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        string name = UniqueName();
        string code = UniqueCode();

        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest(name, code, "Handles sales pipeline"), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.None, result.Failure);
        Assert.NotNull(result.Role);
        Assert.True(result.Role!.IsActive);

        RoleAuditEvent auditEvent = await context.RoleAuditEvents
            .SingleAsync(candidate => candidate.RoleId == result.Role.Id && candidate.EventType == "created");
        Assert.Equal("agent@example.test", auditEvent.ChangedByHandle);
    }

    [Fact]
    public async Task Update_Succeeds_AndProducesOneUpdatedAuditEvent()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        Role role = await CreateRoleAsync(service);

        string newName = UniqueName();
        RoleMutationResult result = await service.UpdateAsync(
            role.Id, new UpdateRoleRequest(newName, role.Code, "Updated description"), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.None, result.Failure);
        Assert.Equal(newName, result.Role!.Name);

        RoleAuditEvent auditEvent = await context.RoleAuditEvents
            .SingleAsync(candidate => candidate.RoleId == role.Id && candidate.EventType == "updated");
        Assert.NotNull(auditEvent);
    }

    [Fact]
    public async Task Activate_And_Deactivate_EachProduceTheirEventType_AndNeverDeleteTheRow()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        Role role = await CreateRoleAsync(service);

        RoleMutationResult deactivated = await service.DeactivateAsync(role.Id, CancellationToken.None);
        Assert.Equal(RoleMutationFailure.None, deactivated.Failure);
        Assert.False(deactivated.Role!.IsActive);

        RoleMutationResult activated = await service.ActivateAsync(role.Id, CancellationToken.None);
        Assert.Equal(RoleMutationFailure.None, activated.Failure);
        Assert.True(activated.Role!.IsActive);

        Assert.Equal(1, await context.RoleAuditEvents.CountAsync(
            candidate => candidate.RoleId == role.Id && candidate.EventType == "deactivated"));
        Assert.Equal(1, await context.RoleAuditEvents.CountAsync(
            candidate => candidate.RoleId == role.Id && candidate.EventType == "activated"));

        // Deactivating never deletes: the role remains readable/listable.
        Role? stillPresent = await service.GetAsync(role.Id, CancellationToken.None);
        Assert.NotNull(stillPresent);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateName_SameOrDifferentCaseOrWhitespace_IsRejected(string variant)
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        string original = UniqueName();
        RoleMutationResult firstResult = await service.CreateAsync(
            new CreateRoleRequest(original, UniqueCode(), null), CancellationToken.None);
        Assert.Equal(RoleMutationFailure.None, firstResult.Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        RoleMutationResult secondResult = await service.CreateAsync(
            new CreateRoleRequest(second, UniqueCode(), null), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.DuplicateName, secondResult.Failure);
    }

    [Fact]
    public async Task DuplicateCode_IsRejected()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        string code = UniqueCode();
        RoleMutationResult firstResult = await service.CreateAsync(
            new CreateRoleRequest(UniqueName(), code, null), CancellationToken.None);
        Assert.Equal(RoleMutationFailure.None, firstResult.Failure);

        RoleMutationResult secondResult = await service.CreateAsync(
            new CreateRoleRequest(UniqueName(), $" {code.ToLowerInvariant()} ", null), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.DuplicateCode, secondResult.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using RoleManagementDbContext firstContext = PostgresTestDatabase.CreateRoleManagementContext();
        await using RoleManagementDbContext secondContext = PostgresTestDatabase.CreateRoleManagementContext();
        string name = UniqueName();
        string code = UniqueCode();
        CreateRoleRequest request = new(name, code, null);

        Task<RoleMutationResult> first = CreateService(firstContext, "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<RoleMutationResult> second = CreateService(secondContext, "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        RoleMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == RoleMutationFailure.None);
        Assert.Single(results, result => result.Failure is RoleMutationFailure.DuplicateName or RoleMutationFailure.DuplicateCode);
    }

    [Fact]
    public async Task UnknownId_OnUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");
        Guid unknownId = Guid.NewGuid();

        Assert.Equal(RoleMutationFailure.NotFound,
            (await service.UpdateAsync(unknownId, new UpdateRoleRequest(UniqueName(), UniqueCode(), null), CancellationToken.None)).Failure);
        Assert.Equal(RoleMutationFailure.NotFound, (await service.ActivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Equal(RoleMutationFailure.NotFound, (await service.DeactivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Null(await service.GetAsync(unknownId, CancellationToken.None));
    }

    [Fact]
    public async Task NullChangedByHandle_IsTolerated_AndDoesNotThrow()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, handle: null);

        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest(UniqueName(), UniqueCode(), null), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.None, result.Failure);
        RoleAuditEvent auditEvent = await context.RoleAuditEvents
            .SingleAsync(candidate => candidate.RoleId == result.Role!.Id);
        Assert.Null(auditEvent.ChangedByHandle);
    }

    [Fact]
    public async Task EmptyDescription_IsAcceptedAsNull()
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        RoleService service = CreateService(context, "agent@example.test");

        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest(UniqueName(), UniqueCode(), "   "), CancellationToken.None);

        Assert.Equal(RoleMutationFailure.None, result.Failure);
        Assert.Null(result.Role!.Description);
    }

    private static async Task<Role> CreateRoleAsync(RoleService service)
    {
        RoleMutationResult result = await service.CreateAsync(
            new CreateRoleRequest(UniqueName(), UniqueCode(), null), CancellationToken.None);
        return result.Role!;
    }

    private static RoleService CreateService(RoleManagementDbContext context, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle));

    private static string UniqueName() => $"Role {Guid.NewGuid():N}";

    private static string UniqueCode() => $"ROLE_{Guid.NewGuid():N}"[..20];

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }
}
