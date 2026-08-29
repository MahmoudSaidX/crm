using System.Net;
using System.Net.Http.Json;

namespace SquadCrm.Api.Tests;

public sealed class AuthenticationBoundaryTests
{
    [Fact]
    public async Task ProtectedEndpoint_RejectsAnonymousRequest()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/auth/me", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidLoginShape_UsesValidationProblemDetailsWithoutEchoingPassword()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        const string password = "short";

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "not-an-email", password },
            CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginEndpoint_IsRateLimitedBeforeAuthenticationWork()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage accepted = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "invalid", password = "short" },
                CancellationToken.None);
            Assert.Equal(HttpStatusCode.BadRequest, accepted.StatusCode);
        }

        using HttpResponseMessage limited = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "invalid", password = "short" },
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task RefreshEndpoint_IsRateLimited()
    {
        await using SquadCrmApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            using HttpResponseMessage rejected = await client.PostAsync(
                "/api/v1/auth/refresh", content: null, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }

        using HttpResponseMessage limited = await client.PostAsync(
            "/api/v1/auth/refresh", content: null, CancellationToken.None);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
