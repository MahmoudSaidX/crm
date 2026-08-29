using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Bootstrap;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class StaffBootstrapTests
{
    public StaffBootstrapTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Bootstrap_RefusesToRunOutsideDevelopment()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BootstrapProgram.CreateOrResetAsync(
                "Production",
                "bootstrap-production@example.test",
                "LocalOnlyPassword!42",
                context,
                new PasswordHasher<StaffUser>(),
                CancellationToken.None));

        Assert.Contains("ASPNETCORE_ENVIRONMENT=Development", exception.Message, StringComparison.Ordinal);
        Assert.False(await context.StaffUsers.AnyAsync(
            user => user.NormalizedEmail == "BOOTSTRAP-PRODUCTION@EXAMPLE.TEST"));
    }

    [Fact]
    public async Task Bootstrap_CreatesActiveUserWhoseCredentialsAuthenticate()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        string email = $"bootstrap-create-{Guid.NewGuid():N}@example.test";
        const string password = "LocalOnlyPassword!42";

        await BootstrapProgram.CreateOrResetAsync(
            "Development",
            email,
            password,
            context,
            new PasswordHasher<StaffUser>(),
            CancellationToken.None);

        StaffUser user = await context.StaffUsers.SingleAsync(
            candidate => candidate.NormalizedEmail == email.ToUpperInvariant());
        Assert.True(user.IsActive);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.NotNull(await CreateAuthenticationService(context).SignInAsync(
            email,
            password,
            rememberSession: false,
            CancellationToken.None));
    }

    [Fact]
    public async Task Bootstrap_ExistingUserResetsPasswordReactivatesAndDoesNotDuplicate()
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        string email = $"bootstrap-reset-{Guid.NewGuid():N}@example.test";
        const string oldPassword = "OldLocalPassword!42";
        const string newPassword = "NewLocalPassword!42";

        await BootstrapProgram.CreateOrResetAsync(
            "Development",
            email,
            oldPassword,
            context,
            new PasswordHasher<StaffUser>(),
            CancellationToken.None);
        StaffUser user = await context.StaffUsers.SingleAsync(
            candidate => candidate.NormalizedEmail == email.ToUpperInvariant());
        user.IsActive = false;
        await context.SaveChangesAsync(CancellationToken.None);

        await BootstrapProgram.CreateOrResetAsync(
            "Development",
            $"  {email.ToUpperInvariant()}  ",
            newPassword,
            context,
            new PasswordHasher<StaffUser>(),
            CancellationToken.None);

        Assert.Equal(1, await context.StaffUsers.CountAsync(
            candidate => candidate.NormalizedEmail == email.ToUpperInvariant()));
        Assert.True(user.IsActive);
        AuthenticationService authentication = CreateAuthenticationService(context);
        Assert.Null(await authentication.SignInAsync(
            email,
            oldPassword,
            rememberSession: false,
            CancellationToken.None));
        Assert.NotNull(await authentication.SignInAsync(
            email,
            newPassword,
            rememberSession: false,
            CancellationToken.None));
    }

    private static AuthenticationService CreateAuthenticationService(StaffIdentityDbContext context) =>
        new(
            context,
            new PasswordHasher<StaffUser>(),
            Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions
            {
                SigningKey = "not-a-real-bootstrap-test-signing-key",
            }));
}
