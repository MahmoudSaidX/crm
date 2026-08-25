namespace SquadCrm.Api.Tests;

/// <summary>CORS is driven entirely by configuration; an empty allow-list blocks everything.</summary>
public sealed class CorsTests
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string UnlistedOrigin = "http://evil.example";

    [Fact]
    public async Task Preflight_FromConfiguredOrigin_EchoesAllowOrigin()
    {
        using var factory = new SquadCrmApiFactory("Development");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendPreflightAsync(client, AllowedOrigin);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? values));
        Assert.Equal(AllowedOrigin, Assert.Single(values!));
    }

    [Fact]
    public async Task Preflight_FromUnlistedOrigin_HasNoAllowOrigin()
    {
        using var factory = new SquadCrmApiFactory("Development");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendPreflightAsync(client, UnlistedOrigin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// Outside Development no origins are configured, so the allow-list is empty and
    /// every cross-origin request — including one that Development would permit — is blocked.
    /// </summary>
    [Fact]
    public async Task Preflight_WithEmptyAllowList_HasNoAllowOrigin()
    {
        using var factory = new SquadCrmApiFactory("Production");
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await SendPreflightAsync(client, AllowedOrigin);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            new Uri("/internal/architecture-fixture/module-info", UriKind.Relative));

        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");

        return await client.SendAsync(request);
    }
}
