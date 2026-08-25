using System.Net;
using System.Text.Json;

namespace SquadCrm.Api.Tests;

/// <summary>
/// The error contract: RFC 9457 Problem Details with a safe <c>traceId</c> and no
/// leakage of exception detail — in Development or otherwise.
/// </summary>
public sealed class ProblemDetailsTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnhandledException_ProducesProblemDetails_WithoutLeakingExceptionDetail(string environment)
    {
        using var factory = new SquadCrmApiFactory(environment, injectFault: true);
        using HttpClient client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(SquadCrmApiFactory.FaultRoute, UriKind.Relative));

        // An Origin header takes the request through the CORS middleware, where the
        // injected fault is raised inside the host's guarded pipeline.
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:4200");

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        Assert.True(root.TryGetProperty("type", out _));
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal(SquadCrmApiFactory.FaultRoute, root.GetProperty("instance").GetString());

        // Lowercase traceId, present and non-empty.
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));

        // No stack trace, no exception message, no inner-exception content.
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SquadCrmApiFactory.SentinelMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SquadCrmApiFactory.SentinelInnerMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }
}
