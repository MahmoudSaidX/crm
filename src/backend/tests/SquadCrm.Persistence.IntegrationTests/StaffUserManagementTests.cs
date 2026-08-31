using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class StaffUserManagementTests
{
    public StaffUserManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Update_Activate_Deactivate_RecordsAuditEvents()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUserService service = new(context, new PasswordHasher<StaffUser>(), new StubCurrentUserAccessor("operator"));
        string email = $"agent-{Guid.NewGuid():N}@example.test";

        StaffUserMutationResult created = await service.CreateAsync(
            new CreateStaffUserRequest(email, "P@ssword123", "Agent One", "Sales", "HQ"), CancellationToken.None);
        Guid userId = created.User!.Id;
        Assert.Equal(StaffUserMutationFailure.None, created.Failure);

        StaffUserMutationResult updated = await service.UpdateAsync(
            userId, new UpdateStaffUserRequest("Agent Uno", "Support", "Branch 2"), CancellationToken.None);
        Assert.Equal(StaffUserMutationFailure.None, updated.Failure);
        Assert.Equal("Agent Uno", updated.User!.DisplayName);

        StaffUserMutationResult deactivated = await service.DeactivateAsync(userId, CancellationToken.None);
        Assert.False(deactivated.User!.IsActive);

        StaffUserMutationResult activated = await service.ActivateAsync(userId, CancellationToken.None);
        Assert.True(activated.User!.IsActive);

        Assert.Equal(
            ["user_created", "user_updated", "user_deactivated", "user_activated"],
            await context.AuthenticationEvents
                .Where(item => item.StaffUserId == userId)
                .OrderBy(item => item.Id)
                .Select(item => item.EventType)
                .ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsDuplicateEmail_CaseAndWhitespaceInsensitive()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUserService service = new(context, new PasswordHasher<StaffUser>(), new StubCurrentUserAccessor("operator"));
        string email = $"dup-{Guid.NewGuid():N}@example.test";

        StaffUserMutationResult first = await service.CreateAsync(
            new CreateStaffUserRequest(email, "P@ssword123", null, null, null), CancellationToken.None);
        StaffUserMutationResult second = await service.CreateAsync(
            new CreateStaffUserRequest($"  {email.ToUpperInvariant()}  ", "P@ssword123", null, null, null),
            CancellationToken.None);

        Assert.Equal(StaffUserMutationFailure.None, first.Failure);
        Assert.Equal(StaffUserMutationFailure.DuplicateEmail, second.Failure);
    }

    [Fact]
    public async Task List_FiltersBySearch_OnEmailOrDisplayName()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUserService service = new(context, new PasswordHasher<StaffUser>(), new StubCurrentUserAccessor("operator"));
        string marker = Guid.NewGuid().ToString("N")[..8];
        await service.CreateAsync(
            new CreateStaffUserRequest($"{marker}@example.test", "P@ssword123", "Findable Person", null, null),
            CancellationToken.None);
        await service.CreateAsync(
            new CreateStaffUserRequest($"other-{Guid.NewGuid():N}@example.test", "P@ssword123", "Someone Else", null, null),
            CancellationToken.None);

        PagedResult<StaffUser> byEmail = await service.ListAsync(new PaginationRequest(), marker, CancellationToken.None);
        PagedResult<StaffUser> byName = await service.ListAsync(new PaginationRequest(), "Findable", CancellationToken.None);

        Assert.Single(byEmail.Items);
        Assert.Single(byName.Items);
    }

    [Fact]
    public async Task Update_And_ActivateDeactivate_UnknownId_ReturnsNotFound()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUserService service = new(context, new PasswordHasher<StaffUser>(), new StubCurrentUserAccessor("operator"));

        Assert.Equal(
            StaffUserMutationFailure.NotFound,
            (await service.UpdateAsync(Guid.NewGuid(), new UpdateStaffUserRequest(null, null, null), CancellationToken.None)).Failure);
        Assert.Equal(
            StaffUserMutationFailure.NotFound,
            (await service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None)).Failure);
    }

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => handle is not null;
        public string? Handle => handle;
    }
}
