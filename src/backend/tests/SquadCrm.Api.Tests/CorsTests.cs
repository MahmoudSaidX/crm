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
    /// An empty allow-list blocks every cross-origin request — including one that
    /// Development would permit. The allow-list is pinned empty here rather than
    /// inherited: environment variables legitimately override <c>appsettings</c>,
    /// so the subject of this test must be stated, not assumed. That the shipped
    /// Development value flows through is proven by the two tests above.
    /// </summary>
    [Fact]
    public async Task Preflight_WithEmptyAllowList_HasNoAllowOrigin()
    {
        using SquadCrmApiFactory factory = SquadCrmApiFactory.WithEmptyCorsAllowList("Production");
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
