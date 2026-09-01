using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Branch routes require explicit view/manage permission policies while
/// preserving the authentication boundary's 401 response for anonymous callers.
/// </summary>
public sealed class BranchEndpointsAuthorizationTests
{
    [Fact]
    public async Task List_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/branches", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/branches/{Guid.NewGuid()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/branches",
            new { code = "SALES", arabicName = "المبيعات", englishName = "Sales", description = (string?)null },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/v1/branches/{Guid.NewGuid()}",
            new { code = "SALES", arabicName = "المبيعات", englishName = "Sales", description = (string?)null },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Activate_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/branches/{Guid.NewGuid()}/activate", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/branches/{Guid.NewGuid()}/deactivate", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
