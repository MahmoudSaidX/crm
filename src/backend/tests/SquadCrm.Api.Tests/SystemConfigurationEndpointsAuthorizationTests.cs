using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Configuration routes require explicit view/manage permission policies while
/// preserving the authentication boundary's 401 response for anonymous callers.
/// </summary>
public sealed class SystemConfigurationEndpointsAuthorizationTests
{
    [Fact]
    public async Task List_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/system-configuration", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/system-configuration/general.company_display_name",
            new { value = "Contoso CRM" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
