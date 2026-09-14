using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
/// Proves the two things about CRM-145 that no service-level test can reach,
/// because both live outside <c>QuickReplyService</c>:
/// <list type="number">
/// <item>The real JSON wire contract. <c>Scope</c> must bind and serialize by
/// NAME. An undecorated enum on the REQUEST record binds only the underlying
/// integer, so a client posting <c>"Global"</c> — including this story's own
/// Angular client — fails before the handler ever runs. Service tests pass C#
/// enum values directly and never see it.</item>
/// <item>The real <c>GlobalQuickReplyAuthorizer</c>, which evaluates the
/// <c>permission:quickreplies.manageglobal</c> policy through the actual
/// authorization pipeline. Every other test stubs that port, so only a real
/// authenticated request proves the global-template rule is genuinely wired to
/// the permission catalog rather than merely to a test double.</item>
/// </list>
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class QuickReplyHttpContractTests
{
    private static readonly AuthenticationOptions TokenOptions = new()
    {
        SigningKey = "not-a-real-http-contract-test-signing-key",
        AccessTokenMinutes = 5,
        RefreshSessionDays = 7,
        RememberedSessionDays = 30,
    };

    public QuickReplyHttpContractTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task CreateGlobal_WithGlobalPermission_BindsAndReturnsNamedScope_NotAnInteger()
    {
        await using QuickReplyHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(
            "quickreplies.view", "quickreplies.manage", "quickreplies.manageglobal");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/quick-replies",
            new
            {
                name = $"Contract {Guid.NewGuid():N}"[..20],
                arabicContent = "مرحباً بك",
                englishContent = "Welcome",
                scope = "Global",
            },
            CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"scope\":\"Global\"", body, StringComparison.Ordinal);
        Assert.Contains("\"isActive\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePersonal_RoundTripsNamedScope_AndOwnerIsTheCaller()
    {
        await using QuickReplyHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(
            "quickreplies.view", "quickreplies.manage");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/quick-replies",
            new
            {
                name = $"Contract {Guid.NewGuid():N}"[..20],
                arabicContent = (string?)null,
                englishContent = "Thanks for reaching out",
                scope = "Personal",
            },
            CancellationToken.None);
        string createBody = await createResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Contains("\"scope\":\"Personal\"", createBody, StringComparison.Ordinal);

        Guid id = JsonDocument.Parse(createBody).RootElement.GetProperty("id").GetGuid();
        using HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/v1/quick-replies/{id}", CancellationToken.None);
        string getBody = await getResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("\"scope\":\"Personal\"", getBody, StringComparison.Ordinal);
        Assert.False(
            JsonDocument.Parse(getBody).RootElement.GetProperty("ownerUserId").ValueKind == JsonValueKind.Null,
            "A personal template must record its owner.");
    }

    [Fact]
    public async Task CreateGlobal_WithoutGlobalPermission_IsForbiddenByTheRealPolicyPipeline()
    {
        await using QuickReplyHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync(
            "quickreplies.view", "quickreplies.manage");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/quick-replies",
            new
            {
                name = $"Contract {Guid.NewGuid():N}"[..20],
                arabicContent = (string?)null,
                englishContent = "Should not be created",
                scope = "Global",
            },
            CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("quickreplies.global_permission_required", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnotherUsersPersonalTemplate_IsNeitherListedNorReadable()
    {
        await using QuickReplyHttpFactory factory = new();
        using HttpClient owner = await factory.CreateAuthenticatedClientAsync(
            "quickreplies.view", "quickreplies.manage");
        string name = $"Contract {Guid.NewGuid():N}"[..20];

        using HttpResponseMessage createResponse = await owner.PostAsJsonAsync(
            "/api/v1/quick-replies",
            new { name, arabicContent = (string?)null, englishContent = "Private", scope = "Personal" },
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid id = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: CancellationToken.None)).GetProperty("id").GetGuid();

        // A DIFFERENT authenticated user, with the same permissions.
        using HttpClient intruder = await factory.CreateAuthenticatedClientAsync(
            "quickreplies.view", "quickreplies.manage", "quickreplies.manageglobal");

        using HttpResponseMessage getResponse = await intruder.GetAsync(
            $"/api/v1/quick-replies/{id}", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using HttpResponseMessage listResponse = await intruder.GetAsync(
            "/api/v1/quick-replies?page=1&pageSize=200", CancellationToken.None);
        string listBody = await listResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.DoesNotContain(name, listBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hosts the real API against the same isolated real Postgres database
    /// <see cref="PostgresTestDatabase"/> already created and migrated, so
    /// authorization runs for real — mirrors <c>CustomerHttpFactory</c>.
    /// </summary>
    private sealed class QuickReplyHttpFactory : WebApplicationFactory<Program>
    {
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
                NormalizedEmail = AuthenticationService.NormalizeEmail($"quick-reply-{Guid.NewGuid():N}@example.test"),
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
                Name = $"Quick Reply Test Role {Guid.NewGuid():N}",
                NormalizedName = $"QUICK REPLY TEST ROLE {Guid.NewGuid():N}",
                Code = $"quick-reply-test-{Guid.NewGuid():N}"[..32],
                NormalizedCode = $"QUICK-REPLY-TEST-{Guid.NewGuid():N}"[..32],
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
}
