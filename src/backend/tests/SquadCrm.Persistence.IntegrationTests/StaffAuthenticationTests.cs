using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class StaffAuthenticationTests
{
    public StaffAuthenticationTests(PostgresTestDatabase database) => _ = database;
    private static readonly AuthenticationOptions Options = new()
    {
        SigningKey = "not-a-real-integration-test-signing-key",
        AccessTokenMinutes = 5,
        RefreshSessionDays = 7,
        RememberedSessionDays = 30,
    };

    [Fact]
    public async Task SignIn_StoresOnlyHash_AndCreatesRevocableSessionAndSafeAuditEvent()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        const string password = "SyntheticPassword!42";
        StaffUser user = await AddUserAsync(context, "agent@example.test", password, isActive: true);
        AuthenticationService service = CreateService(context);

        AuthenticationResult? result = await service.SignInAsync(
            "  AGENT@example.test ", password, rememberSession: false, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.DoesNotContain(password, user.PasswordHash, StringComparison.Ordinal);
        Assert.NotEqual(
            result.RefreshToken,
            await context.RefreshSessions
                .Where(session => session.StaffUserId == user.Id)
                .Select(session => session.TokenHash)
                .SingleAsync());
        AuthenticationEvent authenticationEvent = await context.AuthenticationEvents
            .SingleAsync(candidate => candidate.StaffUserId == user.Id && candidate.EventType == "sign_in");
        Assert.Equal("sign_in", authenticationEvent.EventType);
        Assert.Equal("succeeded", authenticationEvent.Outcome);
        Assert.DoesNotContain("@", authenticationEvent.EventType + authenticationEvent.Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentRefresh_RotatesOnce_RejectsReplay_AndLogoutRevokesReplacement()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUser user = await AddUserAsync(context, $"rotation-{Guid.NewGuid():N}@example.test", "SyntheticPassword!42", true);
        AuthenticationService signInService = CreateService(context);
        AuthenticationResult signedIn = (await signInService.SignInAsync(
            user.NormalizedEmail, "SyntheticPassword!42", false, CancellationToken.None))!;

        await using StaffIdentityDbContext firstContext = PostgresTestDatabase.CreateStaffIdentityContext();
        await using StaffIdentityDbContext secondContext = PostgresTestDatabase.CreateStaffIdentityContext();
        Task<AuthenticationResult?> first = CreateService(firstContext)
            .RefreshAsync(signedIn.RefreshToken, CancellationToken.None);
        Task<AuthenticationResult?> second = CreateService(secondContext)
            .RefreshAsync(signedIn.RefreshToken, CancellationToken.None);
        AuthenticationResult?[] attempts = await Task.WhenAll(first, second);
        AuthenticationResult refreshed = Assert.Single(attempts, result => result is not null)!;

        await using StaffIdentityDbContext logoutContext = PostgresTestDatabase.CreateStaffIdentityContext();
        await CreateService(logoutContext).RevokeAsync(refreshed.RefreshToken, CancellationToken.None);

        Assert.NotEqual(signedIn.RefreshToken, refreshed.RefreshToken);
        Assert.Single(attempts, result => result is null);
        await using StaffIdentityDbContext verificationContext = PostgresTestDatabase.CreateStaffIdentityContext();
        Assert.Equal(2, await verificationContext.RefreshSessions.CountAsync(
            session => session.StaffUserId == user.Id && session.RevokedAtUtc != null));
    }

    [Fact]
    public async Task InactiveUser_CannotSignInOrRefresh()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUser active = await AddUserAsync(context, $"inactive-{Guid.NewGuid():N}@example.test", "SyntheticPassword!42", true);
        AuthenticationService service = CreateService(context);
        AuthenticationResult signedIn = (await service.SignInAsync(
            active.NormalizedEmail, "SyntheticPassword!42", false, CancellationToken.None))!;
        active.IsActive = false;
        await context.SaveChangesAsync(CancellationToken.None);

        Assert.Null(await service.SignInAsync(
            active.NormalizedEmail, "SyntheticPassword!42", false, CancellationToken.None));
        Assert.Null(await service.RefreshAsync(signedIn.RefreshToken, CancellationToken.None));
    }

    private static AuthenticationService CreateService(StaffIdentityDbContext context) =>
        new(context, new PasswordHasher<StaffUser>(), Microsoft.Extensions.Options.Options.Create(Options));

    private static async Task<StaffUser> AddUserAsync(
        StaffIdentityDbContext context,
        string email,
        string password,
        bool isActive)
    {
        StaffUser user = new()
        {
            Id = Guid.NewGuid(),
            NormalizedEmail = AuthenticationService.NormalizeEmail(email),
            PasswordHash = string.Empty,
            IsActive = isActive,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = new PasswordHasher<StaffUser>().HashPassword(user, password);
        context.StaffUsers.Add(user);
        await context.SaveChangesAsync(CancellationToken.None);
        return user;
    }
}
