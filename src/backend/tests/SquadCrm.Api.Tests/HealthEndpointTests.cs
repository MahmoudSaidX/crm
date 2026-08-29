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

    [Fact]
    public async Task Live_ReturnsOkWithoutRunningDependencyChecks()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("postgres", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file_storage", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outbox", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_WhenDatabaseUnavailable_IsGenericAndDoesNotLeakConfiguration()
    {
        using var factory = new SquadCrmApiFactory();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("postgres", body, StringComparison.Ordinal);
        Assert.Contains("outbox", body, StringComparison.Ordinal);
        Assert.DoesNotContain(SquadCrmApiFactory.PlaceholderPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain("squadcrm-tests", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Payload", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Error", body, StringComparison.OrdinalIgnoreCase);
    }
}
