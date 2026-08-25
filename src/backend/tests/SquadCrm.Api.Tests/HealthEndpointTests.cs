using System.Net;
using System.Text.Json;

namespace SquadCrm.Api.Tests;

/// <summary>Liveness endpoint. No database/provider probes are expected here.</summary>
public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsOk_WithHealthyStatus()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
    }
}
