using System.Net;
using System.Text.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Proves that a module's services and routes are reachable through the host —
/// i.e. that explicit <c>IModule</c> registration actually composes.
/// This asserts infrastructure wiring, not a product capability.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    public async Task ArchitectureFixtureModule_EndpointAndServiceAreComposedByTheHost()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/internal/architecture-fixture/module-info", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("ArchitectureFixture", document.RootElement.GetProperty("module").GetString());
        Assert.Equal("registered", document.RootElement.GetProperty("status").GetString());
    }

    /// <summary>
    /// RoleManagement adds no test-only module-info endpoint (unlike
    /// ArchitectureFixture); its composition is instead proven by its real
    /// routes being reachable through the host at all — requiring
    /// authorization (401), never a 404, confirms <c>RoleManagementModule</c>
    /// is present in the registered module set.
    /// </summary>
    [Fact]
    public async Task RoleManagementModule_EndpointsAreComposedByTheHost()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/v1/roles", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
