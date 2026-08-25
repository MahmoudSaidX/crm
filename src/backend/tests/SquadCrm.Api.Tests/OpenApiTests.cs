using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Built-in OpenAPI only, exposed at <c>/openapi/v1.json</c> in Development and
/// nowhere else. There is no Swagger/Scalar UI by decision. The <c>v1</c> segment
/// is the built-in document name, not an API-versioning decision.
/// </summary>
public sealed class OpenApiTests
{
    private static readonly Uri DocumentUri = new("/openapi/v1.json", UriKind.Relative);

    [Fact]
    public async Task OpenApiDocument_IsServed_InDevelopment()
    {
        using var factory = new SquadCrmApiFactory(Environments.Development);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(DocumentUri);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("openapi", out JsonElement version));
        Assert.False(string.IsNullOrWhiteSpace(version.GetString()));
        Assert.True(document.RootElement.TryGetProperty("paths", out _));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task OpenApiDocument_IsNotServed_OutsideDevelopment(string environment)
    {
        using var factory = new SquadCrmApiFactory(environment);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(DocumentUri);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
