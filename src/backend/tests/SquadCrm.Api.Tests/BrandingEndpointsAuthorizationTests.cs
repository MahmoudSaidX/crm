using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Branding admin routes require explicit view/manage permission policies
/// while preserving the authentication boundary's 401 response for
/// anonymous callers. The effective/logo read routes are intentionally
/// anonymous (they render on the pre-login shell) and are exercised in the
/// persistence integration suite, not here.
/// </summary>
public sealed class BrandingEndpointsAuthorizationTests
{
    [Fact]
    public async Task GetSettings_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/branding", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/branding",
            new
            {
                organizationDisplayNameEn = "Acme",
                organizationDisplayNameAr = (string?)null,
                productDisplayNameEn = "Acme CRM",
                productDisplayNameAr = (string?)null,
                themeTokens = (object?)null,
            },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadLogo_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent content = new();

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/branding/logo/primary", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteLogo_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.DeleteAsync(
            "/api/v1/branding/logo/primary", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
