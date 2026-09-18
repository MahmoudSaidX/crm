using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Quick reply routes require explicit view/manage permission policies while
/// preserving the authentication boundary's 401 response for anonymous callers
/// (CRM-145). The scope-dependent half of the rule — the global-template
/// permission and personal ownership — is enforced inside
/// <c>QuickReplyService</c> and covered by the persistence integration suite,
/// which can construct a caller identity.
/// </summary>
public sealed class QuickReplyEndpointsAuthorizationTests
{
    [Fact]
    public async Task List_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/quick-replies", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/quick-replies/{Guid.NewGuid()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/quick-replies",
            new { name = "Greeting", arabicContent = "مرحباً", englishContent = "Hello", scope = "Personal" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/v1/quick-replies/{Guid.NewGuid()}",
            new { name = "Greeting", arabicContent = "مرحباً", englishContent = "Hello" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Activate_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/quick-replies/{Guid.NewGuid()}/activate", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/quick-replies/{Guid.NewGuid()}/deactivate", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resolve_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/quick-replies/{Guid.NewGuid()}/resolve",
            new { ticketId = (Guid?)null },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
