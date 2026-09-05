using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// Proves the real JSON wire representation of the Customer HTTP contract
/// through the actual ASP.NET Core pipeline — a defect where
/// <c>CustomerStatus</c>/<c>CustomerPreferredLanguage</c> serialize as
/// integers (System.Text.Json's default for undecorated enums) instead of
/// the string names the frontend and CRM-125's Acceptance Criteria expect
/// would pass every service-level test (which only ever compares C# enum
/// values, never the actual bytes on the wire) and every authorization test
/// (which only asserts 401 before any body is parsed). Only a real
/// authenticated HTTP round trip, asserted against the raw JSON string,
/// catches it.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class CustomerHttpContractTests
{
    private static readonly AuthenticationOptions TokenOptions = new()
    {
        SigningKey = "not-a-real-http-contract-test-signing-key",
        AccessTokenMinutes = 5,
        RefreshSessionDays = 7,
        RememberedSessionDays = 30,
    };

    public CustomerHttpContractTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task CreateGetUpdateGet_RoundTripsNamedEnumValues_NotIntegers()
    {
        await using CustomerHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync();
        string lastName = $"HttpContract{Guid.NewGuid():N}";

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/customers",
            new
            {
                firstName = "Sara",
                lastName,
                preferredLanguage = "Arabic",
                departmentId = (Guid?)null,
                branchId = (Guid?)null,
            },
            CancellationToken.None);
        string createBody = await createResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Contains("\"preferredLanguage\":\"Arabic\"", createBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Active\"", createBody, StringComparison.Ordinal);
        Guid customerId = System.Text.Json.JsonDocument.Parse(createBody).RootElement.GetProperty("id").GetGuid();
        uint version = System.Text.Json.JsonDocument.Parse(createBody).RootElement.GetProperty("version").GetUInt32();

        using HttpResponseMessage getResponse = await client.GetAsync(
            $"/api/v1/customers/{customerId}", CancellationToken.None);
        string getBody = await getResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("\"preferredLanguage\":\"Arabic\"", getBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Active\"", getBody, StringComparison.Ordinal);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/customers/{customerId}",
            new
            {
                firstName = "Sara",
                lastName,
                preferredLanguage = "English",
                departmentId = (Guid?)null,
                branchId = (Guid?)null,
                status = "Inactive",
                version,
            },
            CancellationToken.None);
        string updateBody = await updateResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Contains("\"preferredLanguage\":\"English\"", updateBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Inactive\"", updateBody, StringComparison.Ordinal);

        using HttpResponseMessage getAfterUpdateResponse = await client.GetAsync(
            $"/api/v1/customers/{customerId}", CancellationToken.None);
        string getAfterUpdateBody = await getAfterUpdateResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("\"preferredLanguage\":\"English\"", getAfterUpdateBody, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"Inactive\"", getAfterUpdateBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Documents a real but SEPARATE, pre-existing, cross-cutting defect
    /// discovered while verifying the CRM-125 enum-contract fix: an invalid
    /// enum string throws <c>BadHttpRequestException</c> (a framework
    /// exception that carries its own 400 status), but
    /// <c>GlobalExceptionHandler</c> (shared <c>BuildingBlocks</c>) treats
    /// every exception identically and always writes 500 — regardless of
    /// enum converters, and regardless of which endpoint or module raises it.
    /// Fixing that handler is a global exception-handling decision outside
    /// "Customer contracts" scope, so this test intentionally asserts today's
    /// actual behavior rather than silently patching a shared handler here.
    /// See the CRM-125 Squad Kit plan deviation note and the publication
    /// report for the follow-up recommendation.
    /// </summary>
    [Fact]
    public async Task UpdateCustomer_InvalidStatusEnumString_CurrentlyReturns500_PreExistingGlobalExceptionHandlingGap()
    {
        await using CustomerHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync();
        string lastName = $"HttpContract{Guid.NewGuid():N}";
        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/customers",
            new
            {
                firstName = "Omar",
                lastName,
                preferredLanguage = (string?)null,
                departmentId = (Guid?)null,
                branchId = (Guid?)null,
            },
            CancellationToken.None);
        Guid customerId = (await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
            cancellationToken: CancellationToken.None)).GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsync(
            $"/api/v1/customers/{customerId}",
            System.Net.Http.Json.JsonContent.Create(new
            {
                firstName = "Omar",
                lastName,
                preferredLanguage = (string?)null,
                departmentId = (Guid?)null,
                branchId = (Guid?)null,
                status = "NotARealStatus",
                version = 0,
            }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, updateResponse.StatusCode);
    }

    [Fact]
    public async Task AddContact_StillRoundTripsNamedContactType_UnchangedByCustomerEnumFix()
    {
        await using CustomerHttpFactory factory = new();
        using HttpClient client = await factory.CreateAuthenticatedClientAsync();
        string lastName = $"HttpContract{Guid.NewGuid():N}";
        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/customers",
            new
            {
                firstName = "Layla",
                lastName,
                preferredLanguage = (string?)null,
                departmentId = (Guid?)null,
                branchId = (Guid?)null,
            },
            CancellationToken.None);
        Guid customerId = (await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(
            cancellationToken: CancellationToken.None)).GetProperty("id").GetGuid();

        using HttpResponseMessage contactResponse = await client.PostAsJsonAsync(
            $"/api/v1/customers/{customerId}/contacts",
            new { type = "Email", value = $"{Guid.NewGuid():N}@example.test", label = (string?)null, isPrimary = true },
            CancellationToken.None);
        string contactBody = await contactResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, contactResponse.StatusCode);
        Assert.Contains("\"type\":\"Email\"", contactBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hosts the real API against the same isolated real Postgres database
    /// <see cref="PostgresTestDatabase"/> already created and migrated for this
    /// test class, so authorization (which queries <c>RoleManagementDbContext</c>
    /// for real) and JSON serialization both run exactly as they do in
    /// production — unlike <c>SquadCrm.Api.Tests</c>'s factory, which
    /// deliberately fakes the database and therefore can only prove the 401
    /// anonymous boundary, never a real authenticated+authorized request.
    /// </summary>
    private sealed class CustomerHttpFactory : WebApplicationFactory<Program>
    {
        private const string ManagePermissionCode = "customers.manage";
        private const string ViewPermissionCode = "customers.view";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // POSTGRES_* is left untouched: it already points at this test
            // class's isolated real database (set by PostgresTestDatabase).
            builder.UseSetting("Authentication:SigningKey", TokenOptions.SigningKey);
            builder.UseSetting("BackgroundProcessing:Enabled", "false");
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        }

        public async Task<HttpClient> CreateAuthenticatedClientAsync()
        {
            (Guid staffUserId, string password) = await SeedStaffUserAsync();
            await GrantCustomerPermissionsAsync(staffUserId);
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
                NormalizedEmail = AuthenticationService.NormalizeEmail($"http-contract-{Guid.NewGuid():N}@example.test"),
                PasswordHash = string.Empty,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            user.PasswordHash = new PasswordHasher<StaffUser>().HashPassword(user, password);
            context.StaffUsers.Add(user);
            await context.SaveChangesAsync(CancellationToken.None);
            return (user.Id, password);
        }

        private static async Task GrantCustomerPermissionsAsync(Guid staffUserId)
        {
            await using RoleManagementDbContext context = PostgresTestDatabase.CreateRoleManagementContext();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Role role = new()
            {
                Id = Guid.NewGuid(),
                Name = $"HTTP Contract Test Role {Guid.NewGuid():N}",
                NormalizedName = $"HTTP CONTRACT TEST ROLE {Guid.NewGuid():N}",
                Code = $"http-contract-test-{Guid.NewGuid():N}"[..32],
                NormalizedCode = $"HTTP-CONTRACT-TEST-{Guid.NewGuid():N}"[..32],
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            context.Roles.Add(role);
            context.RolePermissions.AddRange(
                new RolePermission { RoleId = role.Id, PermissionCode = ManagePermissionCode },
                new RolePermission { RoleId = role.Id, PermissionCode = ViewPermissionCode });
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
