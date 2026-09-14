using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.RoleManagement.Domain.Entities;
using SquadCrm.Modules.RoleManagement.Infrastructure.Persistence;
using SquadCrm.Modules.StaffIdentity.Application.Services;
using SquadCrm.Modules.StaffIdentity.Domain.Entities;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Authentication;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Boots the real API and issues a genuinely authenticated client holding the
/// requested permissions.
/// <para>
/// Query-parameter validation runs as an endpoint filter <i>inside</i> the
/// pipeline, after routing and authorization. A service-level test cannot
/// observe it at all, and an anonymous request never reaches it — so proving a
/// bounds rule requires a real authorized HTTP call, which is what this
/// provides.
/// </para>
/// </summary>
internal sealed class AuthenticatedApiFactory : WebApplicationFactory<Program>
{
    private static readonly AuthenticationOptions TokenOptions = new()
    {
        SigningKey = "not-a-real-list-query-boundary-test-signing-key",
        AccessTokenMinutes = 5,
        RefreshSessionDays = 7,
        RememberedSessionDays = 30,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("Authentication:SigningKey", TokenOptions.SigningKey);
        builder.UseSetting("BackgroundProcessing:Enabled", "false");
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(params string[] permissionCodes)
    {
        (Guid staffUserId, string password) = await SeedStaffUserAsync();
        await GrantPermissionsAsync(staffUserId, permissionCodes);
        string accessToken = await SignInAsync(staffUserId, password);

        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<(Guid StaffUserId, string Password)> SeedStaffUserAsync()
    {
        const string password = "SyntheticPassword!42";
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUser user = new()
        {
            Id = Guid.NewGuid(),
            NormalizedEmail = AuthenticationService.NormalizeEmail($"list-query-{Guid.NewGuid():N}@example.test"),
            PasswordHash = string.Empty,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = new PasswordHasher<StaffUser>().HashPassword(user, password);
        context.StaffUsers.Add(user);
        await context.SaveChangesAsync(CancellationToken.None);
        return (user.Id, password);
    }

    private static async Task GrantPermissionsAsync(Guid staffUserId, IReadOnlyList<string> permissionCodes)
    {
        await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Role role = new()
        {
            Id = Guid.NewGuid(),
            Name = $"List Query Test Role {Guid.NewGuid():N}",
            NormalizedName = $"LIST QUERY TEST ROLE {Guid.NewGuid():N}",
            Code = $"list-query-test-{Guid.NewGuid():N}"[..32],
            NormalizedCode = $"LIST-QUERY-TEST-{Guid.NewGuid():N}"[..32],
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        context.Roles.Add(role);
        context.RolePermissions.AddRange(permissionCodes.Select(
            code => new RolePermission { RoleId = role.Id, PermissionCode = code }));
        context.StaffSubjectRoles.Add(new StaffSubjectRole { StaffSubjectId = staffUserId, RoleId = role.Id });
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<string> SignInAsync(Guid staffUserId, string password)
    {
        await using StaffIdentityDbContext context = PostgresTestDatabase.CreateStaffIdentityContext();
        StaffUser user = await context.StaffUsers.SingleAsync(
            candidate => candidate.Id == staffUserId, CancellationToken.None);
        AuthenticationService service = new(
            context, new PasswordHasher<StaffUser>(), Options.Create(TokenOptions));
        AuthenticationResult result = (await service.SignInAsync(
            user.NormalizedEmail, password, rememberSession: false, CancellationToken.None))!;
        return result.AccessToken;
    }
}
